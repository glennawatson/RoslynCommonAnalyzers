// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests conservative syntax and semantic gates for modern readability replacements.</summary>
public class ModernSyntaxReadabilityAnalysisTests
{
    /// <summary>Checks unsupported UTF-8 source expressions cannot produce replacement syntax.</summary>
    /// <param name="source">The expression to replace.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("value")]
    [Arguments("GetBytes()")]
    [Arguments("GetBytes(1)")]
    [Arguments("GetBytes(value)")]
    [Arguments("GetBytes(\"one\", \"two\")")]
    public async Task Utf8ReplacementRejectsNonLiteralCallsAsync(string source)
    {
        var expression = SyntaxFactory.ParseExpression(source);
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryCreateUtf8Replacement(expression, ModernSyntaxReadabilityAnalysis.Utf8SpanTarget, out var replacement)).IsFalse();
        await Assert.That(replacement).IsNull();
    }

    /// <summary>Checks inferred names require both an inferable expression and the matching explicit label.</summary>
    /// <param name="source">The tuple argument.</param>
    /// <param name="expected">Whether the explicit label is redundant.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("value", false)]
    [Arguments("value: 1", false)]
    [Arguments("other: value", false)]
    [Arguments("value: value", true)]
    public async Task TupleLabelsRequireMatchingInferredNamesAsync(string source, bool expected)
    {
        var tuple = (TupleExpressionSyntax)SyntaxFactory.ParseExpression($"({source}, 0)");
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryGetInferredTupleElementName(tuple.Arguments[0], out _)).IsEqualTo(expected);
    }

    /// <summary>Checks invalid hash shapes clear partial inputs and valid multipliers work on either side.</summary>
    /// <param name="source">The hash expression.</param>
    /// <param name="expectedCount">The number of syntactically accepted inputs.</param>
    /// <param name="safe">Whether the expression preserves value-type receiver semantics.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("(31 * a.GetHashCode()) + b.GetHashCode()", 2, true)]
    [Arguments("(a.GetHashCode() * 397) ^ b.GetHashCode()", 2, true)]
    [Arguments("a.GetHashCode()", 1, false)]
    [Arguments("(a.GetHashCode() * 31) ^ text.GetHashCode()", 2, false)]
    [Arguments("(a.GetHashCode() * 31) ^ missing.GetHashCode()", 2, false)]
    [Arguments("(System.GetHashCode() * 31) ^ a.GetHashCode()", 2, false)]
    [Arguments("(a.GetHashCode() * 31) ^ 1", 0, false)]
    [Arguments("(1 * 31) ^ b.GetHashCode()", 0, false)]
    [Arguments("(a.GetHashCode() * 17) ^ b.GetHashCode()", 0, false)]
    [Arguments("(a.GetHashCode() * factor) ^ b.GetHashCode()", 0, false)]
    [Arguments("(a.GetHashCode() * 31L) ^ b.GetHashCode()", 0, false)]
    [Arguments("(a.GetHashCode() - 31) ^ b.GetHashCode()", 0, false)]
    [Arguments("a.GetHashCode() ^ b.GetHashCode()", 0, false)]
    [Arguments("a.GetHashCode() - b.GetHashCode()", 0, false)]
    [Arguments("(1.GetHashCode() * 31) ^ b.GetHashCode()", 0, false)]
    [Arguments("(a.GetHashCode(1) * 31) ^ b.GetHashCode()", 0, false)]
    public async Task HashInputsRequireSafeSupportedShapeAsync(string source, int expectedCount, bool safe)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ int a, b, factor; string text; int M() => {source}; }}");
        var expression = (await tree.GetRootAsync()).DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryCollectHashInputs(expression, out var inputs)).IsEqualTo(expectedCount >= ModernSyntaxReadabilityAnalysis.HashCodeCombineMinInputs);
        await Assert.That(inputs.Count).IsEqualTo(expectedCount);
        await Assert.That(ModernSyntaxReadabilityAnalysis.HasSafeHashCodeCombineInputs(expression, CreateModel(tree), CancellationToken.None)).IsEqualTo(safe);
    }

    /// <summary>Checks the maximum supported number of hash inputs and the first unsupported count.</summary>
    /// <param name="count">The number of hash inputs.</param>
    /// <param name="expected">Whether Combine can represent the expression.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(8, true)]
    [Arguments(9, false)]
    public async Task HashInputCountMatchesCombineOverloadsAsync(int count, bool expected)
    {
        var source = "a.GetHashCode()";
        for (var i = 1; i < count; i++)
        {
            source = $"(({source}) * 31) ^ a.GetHashCode()";
        }

        var tree = CSharpSyntaxTree.ParseText($"class C {{ int a; int M() => {source}; }}");
        var expression = (await tree.GetRootAsync()).DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryCollectHashInputs(expression, out var inputs)).IsEqualTo(expected);
        await Assert.That(inputs.Count).IsEqualTo(expected ? count : 0);
        await Assert.That(ModernSyntaxReadabilityAnalysis.HasSafeHashCodeCombineInputs(expression, CreateModel(tree), CancellationToken.None)).IsEqualTo(expected);
    }

    /// <summary>Checks tuple element extraction requires consecutive matching fields and an unused temporary.</summary>
    /// <param name="body">The candidate method body.</param>
    /// <param name="expected">Whether deconstruction is safe.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("var pair = (1, 2); var a = pair.Item1; var b = pair.Item2;", true)]
    [Arguments("int x = 0; var pair = (1, 2); var a = pair.Item1; var b = pair.Item2; x++;", true)]
    [Arguments("var pair = (1, 2, 3); var a = pair.Item1; var b = pair.Item2; var c = pair.Item3;", false)]
    [Arguments("var pair = (1, 2); var a = pair.Item2; var b = pair.Item1;", false)]
    [Arguments("var pair = (1, 2); var a = pair.Item1; var b = pair.Item2; Use(pair);", false)]
    [Arguments("var pair = (1, 2); Use(pair); var b = pair.Item2;", false)]
    [Arguments("var pair = (1, 2); var a = pair.Item1;", false)]
    [Arguments("var pair = 1; int a = 1; int b = 2;", false)]
    [Arguments("var pair = (1, 2); int a, b; var c = pair.Item2;", false)]
    [Arguments("var pair = (1, 2); int a; var b = pair.Item2;", false)]
    [Arguments("var pair = (1, 2); var a = (pair).Item1; var b = pair.Item2;", false)]
    [Arguments("var pair = (1, 2); var a = other.Item1; var b = pair.Item2;", false)]
    [Arguments("var pair = (1, 2); var a = pair.ToString; var b = pair.Item2;", false)]
    [Arguments("var pair = (1, 2); var a = pair.Unknown; var b = pair.Item2;", false)]
    [Arguments("var pair = (1, 2); int = pair.Item1; var b = pair.Item2;", false)]
    [Arguments("var pair;", false)]
    [Arguments("var pair = (1, 2), second = (3, 4);", false)]
    [Arguments("switch (1) { case 1: var pair = (1, 2); var a = pair.Item1; var b = pair.Item2; break; }", false)]
    public async Task DeconstructionRequiresMatchingElementLocalsAsync(string body, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {body} }} }}");
        var local = (await tree.GetRootAsync()).DescendantNodes().OfType<LocalDeclarationStatementSyntax>().First(static node => node.Declaration.Variables[0].Identifier.ValueText == "pair");
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryGetDeconstructionCandidate(local, CreateModel(tree), CancellationToken.None, out _)).IsEqualTo(expected);
    }

    /// <summary>Checks an unfinished tuple temporary is rejected before binding its missing name.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task MissingTupleTemporaryNameIsIgnoredAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { int = (1, 2); int a = 1; int b = 2; } }");
        var local = (await tree.GetRootAsync()).DescendantNodes().OfType<LocalDeclarationStatementSyntax>().First();
        await Assert.That(local.Declaration.Variables[0].Identifier.IsMissing).IsTrue();
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryGetDeconstructionCandidate(local, CreateModel(tree), CancellationToken.None, out _)).IsFalse();
    }

    /// <summary>Checks a tuple converted into an ordinary field-bearing type is not deconstructed.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ConvertedTupleDoesNotMatchOrdinaryFieldsAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            class Wrapper
            {
                public int Item1, Item2;
                public static implicit operator Wrapper((int, int) value) => new Wrapper();
            }
            class C
            {
                void M()
                {
                    Wrapper pair = (1, 2);
                    var first = pair.Item1;
                    var second = pair.Item2;
                }
            }
            """);
        var local = (await tree.GetRootAsync()).DescendantNodes().OfType<LocalDeclarationStatementSyntax>().First();
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryGetDeconstructionCandidate(local, CreateModel(tree), CancellationToken.None, out _)).IsFalse();
    }

    /// <summary>Checks swap recognition rejects mismatched assignments and later uses of the temporary.</summary>
    /// <param name="body">The statements beginning with a temporary.</param>
    /// <param name="expected">Whether the statements form a local swap.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("var temp = left; left = right; right = temp;", true)]
    [Arguments("var temp = left; left = right; right = temp; left++;", true)]
    [Arguments("var temp = left; left = right; right = temp; Use(temp);", false)]
    [Arguments("var temp = left; left += right; right = temp;", false)]
    [Arguments("var temp = left; other = right; right = temp;", false)]
    [Arguments("var temp = left; left = right; other = temp;", false)]
    [Arguments("var temp = left; left = right; right = other;", false)]
    [Arguments("var temp = left; this.left = right; right = temp;", false)]
    [Arguments("var temp = left; left = right + 1; right = temp;", false)]
    [Arguments("var temp = left; left = field; field = temp;", false)]
    [Arguments("var temp = field; field = right; right = temp;", false)]
    [Arguments("int temp = left, spare = right; left = right; right = temp;", false)]
    [Arguments("int temp; left = right; right = temp;", false)]
    [Arguments("var temp = left;", false)]
    [Arguments("switch (1) { case 1: var temp = left; left = right; right = temp; break; }", false)]
    public async Task SwapRequiresExactAssignmentsAsync(string body, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ int field; void M(int left, int right, int other) {{ {body} }} }}");
        var local = (await tree.GetRootAsync()).DescendantNodes().OfType<LocalDeclarationStatementSyntax>().First();
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryGetTupleSwapCandidate(local, CreateModel(tree), CancellationToken.None, out _, out _)).IsEqualTo(expected);
    }

    /// <summary>Checks similar encoding calls do not pass the exact UTF-8 binding check.</summary>
    /// <param name="expression">The candidate invocation.</param>
    /// <param name="expected">Whether it is the supported framework call.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("System.Text.Encoding.UTF8.GetBytes(\"x\")", true)]
    [Arguments("System.Text.Encoding.Unicode.GetBytes(\"x\")", false)]
    [Arguments("System.Text.Encoding.UTF8.GetBytes(new char[] { 'x' })", false)]
    [Arguments("System.Text.Encoding.UTF8.GetBytes(\"x\", 0, 1, new byte[1], 0)", false)]
    [Arguments("System.Text.Encoding.UTF8.GetString(new byte[1])", false)]
    [Arguments("GetBytes(\"x\")", false)]
    [Arguments("Other.UTF8.GetBytes(\"x\")", false)]
    public async Task Utf8CallsRequireTheFrameworkSymbolsAsync(string expression, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ object M() => {expression}; }}");
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        await Assert.That(ModernSyntaxReadabilityAnalysis.IsEncodingUtf8GetBytes(invocation, CreateModel(tree), CancellationToken.None)).IsEqualTo(expected);
    }

    /// <summary>Checks conversion targets distinguish byte arrays, read-only byte spans, and unsupported types.</summary>
    /// <param name="type">The target type.</param>
    /// <param name="expected">The expected replacement target.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("byte[]", "Array")]
    [Arguments("System.ReadOnlySpan<byte>", "Span")]
    [Arguments("object", "")]
    [Arguments("System.Span<byte>", "")]
    [Arguments("System.ReadOnlySpan<int>", "")]
    [Arguments("int[]", "")]
    public async Task Utf8TargetsUseConvertedTypeAsync(string type, string expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ {type} M() => System.Text.Encoding.UTF8.GetBytes(\"x\"); }}");
        var expression = (await tree.GetRootAsync()).DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        await Assert.That(ModernSyntaxReadabilityAnalysis.TryGetUtf8Target(expression, CreateModel(tree), CancellationToken.None, out var target)).IsEqualTo(expected.Length > 0);
        await Assert.That(target).IsEqualTo(expected);
    }

    /// <summary>Checks only a parameterless integer-returning GetHashCode body is recognized.</summary>
    /// <param name="member">The containing member.</param>
    /// <param name="expected">Whether the body belongs to the recognized hash method.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("public override int GetHashCode() => 0;", true)]
    [Arguments("int GetHashCode(int value) => 0;", false)]
    [Arguments("public new long GetHashCode() => 0;", false)]
    [Arguments("int Value => 0;", false)]
    public async Task HashBodyRequiresTheExpectedSignatureAsync(string member, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ {member} }}");
        var expression = (await tree.GetRootAsync()).DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        await Assert.That(ModernSyntaxReadabilityAnalysis.IsGetHashCodeBody(expression, CreateModel(tree), CancellationToken.None)).IsEqualTo(expected);
    }

    /// <summary>Creates a semantic model using the shared cached runtime references.</summary>
    /// <param name="tree">The source syntax tree.</param>
    /// <returns>The semantic model.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SemanticModel CreateModel(SyntaxTree tree) =>
        CSharpCompilation.Create("Readability", [tree], RuntimeMetadataReferences.Platform).GetSemanticModel(tree);
}
