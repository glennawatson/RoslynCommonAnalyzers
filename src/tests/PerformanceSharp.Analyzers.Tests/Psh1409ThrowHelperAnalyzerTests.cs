// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests throw-helper classification and semantic rejection boundaries.</summary>
public class Psh1409ThrowHelperAnalyzerTests
{
    /// <summary>The throw-helper diagnostic identifier.</summary>
    private const string DiagnosticId = "PSH1409";

    /// <summary>Checks every numeric comparison orientation and zero specialization.</summary>
    /// <param name="condition">The comparison.</param>
    /// <param name="helper">The expected helper name.</param>
    /// <param name="operand">The expected second operand.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("x < 0", "ThrowIfNegative", null)]
    [Arguments("0 > x", "ThrowIfNegative", null)]
    [Arguments("x <= 0", "ThrowIfNegativeOrZero", null)]
    [Arguments("0 >= x", "ThrowIfNegativeOrZero", null)]
    [Arguments("x == 0", "ThrowIfZero", null)]
    [Arguments("0 == x", "ThrowIfZero", null)]
    [Arguments("1 < x", "ThrowIfGreaterThan", "1")]
    [Arguments("1 <= x", "ThrowIfGreaterThanOrEqual", "1")]
    [Arguments("x < limit", "ThrowIfLessThan", "limit")]
    [Arguments("x <= limit", "ThrowIfLessThanOrEqual", "limit")]
    [Arguments("x == limit", "ThrowIfEqual", "limit")]
    [Arguments("1 != x", "ThrowIfNotEqual", "1")]
    [Arguments("x != 0", null, null)]
    [Arguments("x + 0", null, null)]
    [Arguments("y < z", null, null)]
    [Arguments("flag", null, null)]
    public async Task ComparisonClassificationPreservesOrientationAsync(string condition, string? helper, string? operand)
    {
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement($"if ({condition}) throw new ArgumentOutOfRangeException(nameof(x));");
        var shape = Psh1409ThrowHelperAnalyzer.TryClassify(statement);
        await Assert.That(shape?.HelperName).IsEqualTo(helper);
        await Assert.That(shape?.Operand?.ToString()).IsEqualTo(operand);
        await Assert.That(shape?.Value.ToString()).IsEqualTo(helper is null ? null : "x");
    }

    /// <summary>Checks null and string guards accept only the documented parameter-name syntax.</summary>
    /// <param name="source">The guard.</param>
    /// <param name="helper">The selected helper, or null.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("if (null == x) throw new global::ArgumentNullException(\"x\");", "ThrowIfNull")]
    [Arguments("if (x == null) throw new System.ArgumentNullException(nameof(x), \"message\");", "ThrowIfNull")]
    [Arguments("if (x is null) { throw new ArgumentNullException(nameof(x)); }", "ThrowIfNull")]
    [Arguments("if (x is 1) throw new ArgumentNullException(nameof(x));", null)]
    [Arguments("if (x is not null) throw new ArgumentNullException(nameof(x));", null)]
    [Arguments("if (this.x == null) throw new ArgumentNullException(nameof(x));", null)]
    [Arguments("if (null == this.x) throw new ArgumentNullException(nameof(x));", null)]
    [Arguments("if (x == y) throw new ArgumentNullException(nameof(x));", null)]
    [Arguments("if (x == null) throw new ArgumentNullException();", null)]
    [Arguments("if (x == null) throw new ArgumentNullException;", null)]
    [Arguments("if (x == null) throw new ArgumentNullException(nameof(x), 1, 2);", null)]
    [Arguments("if (x == null) throw new ArgumentNullException(GetName());", null)]
    [Arguments("if (x == null) throw new ArgumentException(nameof(x));", null)]
    [Arguments("if (string.IsNullOrEmpty(x)) throw new ArgumentNullException(nameof(x));", "ThrowIfNullOrEmpty")]
    [Arguments("if (string.IsNullOrWhiteSpace(x)) throw new ArgumentException(\"message\", \"x\");", "ThrowIfNullOrWhiteSpace")]
    [Arguments("if (string.IsNullOrEmpty(x)) throw new ArgumentException;", null)]
    [Arguments("if (string.IsNullOrEmpty(x)) throw new ArgumentException { };", null)]
    [Arguments("if (string.IsNullOrEmpty(x)) throw new ArgumentException();", null)]
    [Arguments("if (string.IsNullOrEmpty(x)) throw new ArgumentException(\"other\");", null)]
    [Arguments("if (string.IsNullOrEmpty(Get())) throw new ArgumentException(nameof(x));", null)]
    [Arguments("if (IsNullOrEmpty(x)) throw new ArgumentException(nameof(x));", null)]
    [Arguments("if (string.Other(x)) throw new ArgumentException(nameof(x));", null)]
    [Arguments("if (flag) throw new ObjectDisposedException(nameof(C));", "ThrowIf")]
    [Arguments("if (flag) throw new ObjectDisposedException(GetType().Name);", "ThrowIf")]
    [Arguments("if (flag) throw new ObjectDisposedException(this.GetType().Name);", "ThrowIf")]
    [Arguments("if (flag) throw new ObjectDisposedException(typeof(C).Name);", "ThrowIf")]
    [Arguments("if (flag) throw new ObjectDisposedException(\"C\");", null)]
    [Arguments("if (flag) throw new ObjectDisposedException();", null)]
    [Arguments("if (x < 0) throw new ArgumentOutOfRangeException();", null)]
    [Arguments("if (flag) throw new Exception();", null)]
    [Arguments("if (flag) throw new int();", null)]
    [Arguments("if (flag) throw error;", null)]
    [Arguments("if (flag) { Log(); throw new Exception(); }", null)]
    [Arguments("if (x is null) throw new ArgumentNullException(nameof(x)); else Log();", null)]
    public async Task GuardClassificationRejectsUnsupportedShapesAsync(string source, string? helper)
    {
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement(source);
        await Assert.That(Psh1409ThrowHelperAnalyzer.TryClassify(statement)?.HelperName).IsEqualTo(helper);
    }

    /// <summary>Checks unknown exception names and non-name type syntax have no exception index.</summary>
    /// <param name="creation">The exception construction.</param>
    /// <param name="expected">The exception index.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("new ArgumentNullException()", 0)]
    [Arguments("new System.ArgumentException()", 1)]
    [Arguments("new global::ObjectDisposedException()", 2)]
    [Arguments("new ArgumentOutOfRangeException()", 3)]
    [Arguments("new Exception()", -1)]
    [Arguments("new int()", -1)]
    public async Task ExceptionIndexUsesRightmostTypeNameAsync(string creation, int expected)
    {
        var expression = (ObjectCreationExpressionSyntax)SyntaxFactory.ParseExpression(creation);
        await Assert.That(Psh1409ThrowHelperAnalyzer.GetExceptionIndex(expression)).IsEqualTo(expected);
    }

    /// <summary>Checks numeric guards report only for supported primitive value types.</summary>
    /// <param name="type">The checked parameter type.</param>
    /// <param name="expected">Whether a diagnostic should be emitted.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("sbyte", true)]
    [Arguments("byte", true)]
    [Arguments("short", true)]
    [Arguments("ushort", true)]
    [Arguments("int", true)]
    [Arguments("uint", true)]
    [Arguments("long", true)]
    [Arguments("ulong", true)]
    [Arguments("float", true)]
    [Arguments("double", true)]
    [Arguments("decimal", true)]
    [Arguments("int?", false)]
    [Arguments("string", false)]
    [Arguments("char", false)]
    [Arguments("Missing", false)]
    public async Task NumericGuardRequiresSupportedValueTypeAsync(string type, bool expected)
    {
        var compilation = Compile($"class C {{ void M({type} x) {{ if (x == 1) throw new System.ArgumentOutOfRangeException(nameof(x)); }} }}");
        var diagnostics = await compilation.WithAnalyzers([new Psh1409ThrowHelperAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expected ? 1 : 0);
        if (expected)
        {
            await Assert.That(diagnostics[0].Id).IsEqualTo(DiagnosticId);
        }
    }

    /// <summary>Checks binding rejects shadow exceptions, nullable values, unresolved values, and unconstrained generics.</summary>
    /// <param name="source">The complete compilation source.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("""
        class ArgumentNullException : System.Exception { public ArgumentNullException(string name) {} }
        class C { void M(string x) { if (x is null) throw new ArgumentNullException(nameof(x)); } }
        """)]
    [Arguments("class C { void M(int? x) { if (x is null) throw new System.ArgumentNullException(nameof(x)); } }")]
    [Arguments("class C { void M<T>(T x) { if (x is null) throw new System.ArgumentNullException(nameof(x)); } }")]
    [Arguments("class C { void M() { if (x == 1) throw new System.ArgumentOutOfRangeException(\"x\"); } }")]
    [Arguments("class C { void x() {} void M() { if (x == 1) throw new System.ArgumentOutOfRangeException(\"x\"); } }")]
    public async Task SemanticNearMissesDoNotReportAsync(string source)
    {
        var diagnostics = await Compile(source).WithAnalyzers([new Psh1409ThrowHelperAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    /// <summary>Verifies unresolved null operands do not produce throw-helper diagnostics.</summary>
    /// <param name="source">The guard with an operand that does not bind.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("class C { void M() { if (x is null) throw new System.ArgumentNullException(\"x\"); } }")]
    [Arguments("class C { void M() { if (x == null) throw new System.ArgumentNullException(\"x\"); } }")]
    [Arguments("class C { void M() { if (null == x) throw new System.ArgumentNullException(\"x\"); } }")]
    [Arguments("class C { void M(Missing x) { if (x is null) throw new System.ArgumentNullException(nameof(x)); } }")]
    [Arguments("class C { void x() {} void M() { if (x is null) throw new System.ArgumentNullException(\"x\"); } }")]
    public async Task UnresolvedNullOperandDoesNotReportAsync(string source)
    {
        var diagnostics = await Compile(source).WithAnalyzers([new Psh1409ThrowHelperAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Checks ordinary reference types and constrained generic parameters retain null-guard diagnostics.</summary>
    /// <param name="source">The complete compilation source.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("class Lock {} class C { void M(Lock x) { if (x is null) throw new System.ArgumentNullException(nameof(x)); } }")]
    [Arguments("class C { void M<T>(T x) where T : class { if (x is null) throw new System.ArgumentNullException(nameof(x)); } }")]
    [Arguments("class C { void M(string x) { if (x == null) throw new System.ArgumentNullException(nameof(x)); } }")]
    [Arguments("class C { void M(object x) { if (null == x) throw new System.ArgumentNullException(nameof(x)); } }")]
    public async Task ReferenceNullGuardsRemainCandidatesAsync(string source)
    {
        var diagnostics = await Compile(source).WithAnalyzers([new Psh1409ThrowHelperAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo(DiagnosticId);
    }

    /// <summary>Checks an incomplete framework cannot offer a helper for a missing exception type.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task MissingFrameworkExceptionDoesNotReportAsync()
    {
        const string Source = "class C { void M(object x) { if (x is null) throw new System.ArgumentNullException(nameof(x)); } }";
        var compilation = CSharpCompilation.Create("NoFramework", [CSharpSyntaxTree.ParseText(Source)]);
        var diagnostics = await compilation.WithAnalyzers([new Psh1409ThrowHelperAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    /// <summary>Checks string guard receivers use qualified BCL names or matching inherited aliases as required.</summary>
    /// <param name="prefix">Imports and helper declarations.</param>
    /// <param name="expected">The selected receiver spelling.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("using System;", "ArgumentException")]
    [Arguments("", "global::System.ArgumentException")]
    [Arguments("class ArgumentException {}", "global::System.ArgumentException")]
    [Arguments(
        """
        using ArgumentExceptionHelper = Derived;
        class Base { public static void ThrowIfNullOrWhiteSpace(string x) {} }
        class Derived : Base {}
        """,
        "ArgumentExceptionHelper")]
    [Arguments("using ArgumentExceptionHelper = Empty; class Empty {}", "global::System.ArgumentException")]
    public async Task StringGuardReceiverRespectsAliasesAndShadowingAsync(string prefix, string expected)
    {
        var compilation = Compile($"{prefix} class C {{ void M(string x) {{ if (string.IsNullOrWhiteSpace(x)) throw new System.ArgumentNullException(nameof(x)); }} }}");
        var tree = compilation.SyntaxTrees[0];
        var root = await tree.GetRootAsync();
        var statement = root.DescendantNodes().OfType<IfStatementSyntax>().Single();
        var shape = Psh1409ThrowHelperAnalyzer.TryClassify(statement)!.Value;
        await Assert.That(Psh1409ThrowHelperAnalyzer.TryGetHelperReceiver(compilation.GetSemanticModel(tree), statement.SpanStart, shape)).IsEqualTo(expected);
        var diagnostics = await compilation.WithAnalyzers([new Psh1409ThrowHelperAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains($"{expected}.ThrowIfNullOrWhiteSpace");
    }

    /// <summary>Compiles source using cached runtime references.</summary>
    /// <param name="source">The source to bind.</param>
    /// <returns>The compilation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CSharpCompilation Compile(string source) => CSharpCompilation.Create("ThrowGuards", [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
}
