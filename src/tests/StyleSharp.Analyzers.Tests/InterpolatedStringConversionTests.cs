// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the syntax and binding contracts shared by interpolation analysis and fixes.</summary>
public class InterpolatedStringConversionTests
{
    /// <summary>Checks format and concat candidates require a supported receiver and member shape.</summary>
    /// <param name="source">The candidate invocation.</param>
    /// <param name="format">Whether it has a composite-format shape.</param>
    /// <param name="concat">Whether it has a literal concatenation-call shape.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Format(\"{0}\", a)", true, false)]
    [Arguments("String.Format(\"{0}\", a)", true, false)]
    [Arguments("System.String.Format(\"{0}\", a)", true, false)]
    [Arguments("string.Format(\"text\")", false, false)]
    [Arguments("Format(\"{0}\", a)", false, false)]
    [Arguments("pointer->Format(\"{0}\", a)", false, false)]
    [Arguments("Formatter.Format(\"{0}\", a)", false, false)]
    [Arguments("string.Join(\"{0}\", a)", false, false)]
    [Arguments("string.Concat(\"text\", a)", false, true)]
    [Arguments("string.Concat(a, \"text\")", false, true)]
    [Arguments("String.Concat(a, \"text\")", false, true)]
    [Arguments("System.String.Concat(a, \"text\")", false, true)]
    [Arguments("string.Concat(\"text\")", false, false)]
    [Arguments("Concat(\"text\", a)", false, false)]
    [Arguments("pointer->Concat(\"text\", a)", false, false)]
    [Arguments("Formatter.Concat(\"text\", a)", false, false)]
    [Arguments("string.Concat(a, b)", false, false)]
    public async Task InvocationShapesRequireTheExpectedStringMemberAsync(string source, bool format, bool concat)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(source);

        await Assert.That(InterpolatedStringConversion.IsFormatShape(invocation)).IsEqualTo(format);
        await Assert.That(InterpolatedStringConversion.IsConcatShape(invocation)).IsEqualTo(concat);
    }

    /// <summary>Checks composite formats reject malformed references, alignments, and format clauses.</summary>
    /// <param name="format">The runtime composite format text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{2147483648}")]
    [Arguments("{999999999999999999999}")]
    [Arguments("{1}")]
    [Arguments("{}")]
    [Arguments("{a}")]
    [Arguments("{/}")]
    [Arguments("{0x}")]
    [Arguments("{0,")]
    [Arguments("{0,-}")]
    [Arguments("{0,+5}")]
    [Arguments("{0,5x}")]
    [Arguments("{0,5")]
    [Arguments("{0:\"}")]
    [Arguments("{0:\\}")]
    [Arguments("{0:\t}")]
    [Arguments("{0:\n}")]
    [Arguments("{0:{x}")]
    [Arguments("{0:X")]
    [Arguments("{0")]
    [Arguments("{")]
    [Arguments("}")]
    [Arguments("}x")]
    [Arguments("{{")]
    [Arguments("{0}{0}")]
    [Arguments("")]
    [Arguments("\u2028{0}")]
    public async Task UnsupportedCompositeFormatTextHasNoConversionAsync(string format)
    {
        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(format));
        var tree = CreateTree($"string.Format({literal}, a)");
        var invocation = (InvocationExpressionSyntax)GetExpression(await tree.GetRootAsync());

        await Assert.That(InterpolatedStringConversion.TryConvertFormat(CreateModel(tree), invocation, CancellationToken.None)).IsNull();
    }

    /// <summary>Checks well-formed clause spacing is normalized while escapes and values are retained.</summary>
    /// <param name="format">The runtime composite format text.</param>
    /// <param name="expected">The resulting interpolated expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{0   }", "$\"{a}\"")]
    [Arguments("{0, -5   :X}", "$\"{a,-5:X}\"")]
    [Arguments("{0,0}", "$\"{a,0}\"")]
    [Arguments("{{{0}}}", "$\"{{{a}}}\"")]
    [Arguments("{00}", "$\"{a}\"")]
    [Arguments("\"\\\t\r\n\u0001{0}", "$\"\\\"\\\\\\t\\r\\n\\u0001{a}\"")]
    public async Task CompositeFormatConversionPreservesTextAndClausesAsync(string format, string expected)
    {
        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(format));
        var tree = CreateTree($"string.Format({literal}, a)");
        var invocation = (InvocationExpressionSyntax)GetExpression(await tree.GetRootAsync());

        await Assert.That(InterpolatedStringConversion.TryConvertFormat(CreateModel(tree), invocation, CancellationToken.None)?.ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks conversion revalidates the binding and positional arguments independently of syntax filtering.</summary>
    /// <param name="source">The candidate format invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Format(\"only a format\")")]
    [Arguments("string.Format(\"{0}\", ref a)")]
    [Arguments("string.Format(format: \"{0}\", arg0: a)")]
    [Arguments("string.Format<string>(\"{0}\", a)")]
    [Arguments("string.Format(System.Globalization.CultureInfo.InvariantCulture, \"{0}\", a)")]
    [Arguments("string.Format(a, b)")]
    [Arguments("string.Format(@\"{0}\", a)")]
    [Arguments("string.Format(\"\"\"{0}\"\"\", a)")]
    [Arguments("string.Format(\"{0}\", values)")]
    public async Task UnsupportedFormatInvocationHasNoConversionAsync(string source)
    {
        var tree = CreateTree(source);
        var invocation = (InvocationExpressionSyntax)GetExpression(await tree.GetRootAsync());

        await Assert.That(InterpolatedStringConversion.TryConvertFormat(CreateModel(tree), invocation, CancellationToken.None)).IsNull();
    }

    /// <summary>Checks concat conversion handles string overloads and rejects unsupported operands and bindings.</summary>
    /// <param name="source">The candidate concat invocation.</param>
    /// <param name="expected">The replacement expression, or null when no conversion exists.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Concat(a)", null)]
    [Arguments("string.Concat(a, b)", null)]
    [Arguments("string.Concat(\"x\", ref a)", null)]
    [Arguments("string.Concat(str0: \"x\", str1: a)", null)]
    [Arguments("string.Concat<int>(\"x\", a)", null)]
    [Arguments("string.Concat(\"x\", n)", null)]
    [Arguments("string.Concat(\"x\", a, b, a, b)", null)]
    [Arguments("string.Concat(@\"x\", a)", null)]
    [Arguments("string.Concat(\"\"\"x\"\"\", a)", null)]
    [Arguments("string.Concat(\"\\u2028\", a)", null)]
    [Arguments("string.Concat(\"x\", flag ? a : b)", "$\"x{(flag ? a : b)}\"")]
    [Arguments("string.Concat(a, \"\", b, \"x\")", "$\"{a}{b}x\"")]
    public async Task ConcatConversionRequiresSafeStringArgumentsAsync(string source, string? expected)
    {
        var tree = CreateTree(source);
        var invocation = (InvocationExpressionSyntax)GetExpression(await tree.GetRootAsync());

        await Assert.That(InterpolatedStringConversion.TryConvertConcat(CreateModel(tree), invocation, CancellationToken.None)?.ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks lookalike methods do not acquire the framework's formatting semantics.</summary>
    /// <param name="declaration">A type whose spelling resembles the framework string type.</param>
    /// <param name="source">The format or concat invocation.</param>
    /// <param name="isFormat">Whether to exercise composite formatting.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class String { public static string Format(string format, object value) => format; }", "String.Format(\"{0}\", a)", true)]
    [Arguments("class String { public static string Concat(string a, string b) => a; }", "String.Concat(\"x\", a)", false)]
    public async Task StringNamedLookalikeHasNoConversionAsync(string declaration, string source, bool isFormat)
    {
        var tree = CSharpSyntaxTree.ParseText($"{declaration} class C {{ string M(string a) => {source}; }}");
        var method = (await tree.GetRootAsync()).DescendantNodes().OfType<MethodDeclarationSyntax>().Last();
        var invocation = (InvocationExpressionSyntax)method.ExpressionBody!.Expression;
        var model = CreateModel(tree);
        var result = isFormat
            ? InterpolatedStringConversion.TryConvertFormat(model, invocation, CancellationToken.None)
            : InterpolatedStringConversion.TryConvertConcat(model, invocation, CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    /// <summary>Checks binary conversion rejects arithmetic and incomplete chains but keeps string concatenation.</summary>
    /// <param name="source">The candidate binary expression.</param>
    /// <param name="expected">The replacement expression, or null for an unsupported chain.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("n - m", null)]
    [Arguments("a + b", null)]
    [Arguments("\"a\" + \"b\"", null)]
    [Arguments("n + m + \"x\"", null)]
    [Arguments("'a' + 'b' + \"x\"", null)]
    [Arguments("1 + 2 + \"x\"", null)]
    [Arguments("@\"x\" + a", null)]
    [Arguments("\"\"\"x\"\"\" + a", null)]
    [Arguments("\"\\u2028\" + a", null)]
    [Arguments("\"x\" + a", "$\"x{a}\"")]
    [Arguments("a + \"x\"", "$\"{a}x\"")]
    [Arguments("a + b + \"x\"", "$\"{a}{b}x\"")]
    [Arguments("\"x\" + (flag ? a : b)", "$\"x{(flag ? a : b)}\"")]
    public async Task BinaryConversionRequiresStringConcatenationThroughoutAsync(string source, string? expected)
    {
        var tree = CreateTree(source);
        var binary = (BinaryExpressionSyntax)GetExpression(await tree.GetRootAsync());

        await Assert.That(InterpolatedStringConversion.TryConvertConcatenation(CreateModel(tree), binary, CancellationToken.None)?.ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks the conversion rejects an inner chain whose outer addition owns the rewrite.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InnerConcatenationHasNoIndependentConversionAsync()
    {
        var tree = CreateTree("\"x\" + a + b");
        var outer = (BinaryExpressionSyntax)GetExpression(await tree.GetRootAsync());
        var inner = (BinaryExpressionSyntax)outer.Left;

        await Assert.That(InterpolatedStringConversion.IsConcatenationCandidate(inner)).IsFalse();
        await Assert.That(InterpolatedStringConversion.TryConvertConcatenation(CreateModel(tree), inner, CancellationToken.None)).IsNull();
    }

    /// <summary>Checks a user-defined operator returning string cannot be flattened as string concatenation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UserDefinedStringProducingAdditionHasNoConversionAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { public static string operator +(C value, string text) => text; string M(C value) => value + \"x\"; }");
        var binary = (await tree.GetRootAsync()).DescendantNodes().OfType<BinaryExpressionSyntax>().Single();

        await Assert.That(InterpolatedStringConversion.TryConvertConcatenation(CreateModel(tree), binary, CancellationToken.None)).IsNull();
    }

    /// <summary>Creates a candidate with ordinary string, numeric, and formatting parameters in scope.</summary>
    /// <param name="expression">The returned expression.</param>
    /// <returns>The parsed compilation tree.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxTree CreateTree(string expression) =>
        CSharpSyntaxTree.ParseText($"class C {{ string M(string a, string b, int n, int m, bool flag, object[] values) => {expression}; }}");

    /// <summary>Retrieves the sole expression-bodied method's result.</summary>
    /// <param name="root">The parsed compilation root.</param>
    /// <returns>The candidate expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionSyntax GetExpression(SyntaxNode root) =>
        root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;

    /// <summary>Creates a semantic model using shared cached platform references.</summary>
    /// <param name="tree">The compilation tree.</param>
    /// <returns>The bound model used by the conversion.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SemanticModel CreateModel(SyntaxTree tree) =>
        CSharpCompilation.Create("InterpolationConversion", [tree], RuntimeMetadataReferences.Platform).GetSemanticModel(tree);
}
