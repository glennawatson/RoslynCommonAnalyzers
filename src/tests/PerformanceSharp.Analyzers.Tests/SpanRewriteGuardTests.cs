// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests when span rewrites preserve expression semantics and name binding.</summary>
public class SpanRewriteGuardTests
{
    /// <summary>The cached reference set for source-defined framework lookalikes.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies only literals and stable name paths can be evaluated repeatedly.</summary>
    /// <param name="source">The expression to classify.</param>
    /// <param name="expected">Whether repeated evaluation is permitted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value", true)]
    [Arguments("this", true)]
    [Arguments("base", true)]
    [Arguments("int", true)]
    [Arguments("42", true)]
    [Arguments("this.Value.Length", true)]
    [Arguments("((value))", true)]
    [Arguments("Get().Value", false)]
    [Arguments("(Get())", false)]
    [Arguments("items[0]", false)]
    [Arguments("pointer->Value", false)]
    public async Task RepeatabilityRejectsExpressionsThatCanDoWorkAsync(string source, bool expected)
    {
        var expression = SyntaxFactory.ParseExpression(source);

        await Assert.That(SpanRewriteGuard.IsRepeatable(expression)).IsEqualTo(expected);
    }

    /// <summary>Verifies ancestry recognizes queries and expression lambdas without rejecting delegate lambdas.</summary>
    /// <param name="source">A compilation containing one numeric literal to inspect.</param>
    /// <param name="expected">Whether the literal is inside a restricted expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int M() => 42; }", false)]
    [Arguments("System.Console.WriteLine(42);", false)]
    [Arguments("using System; class C { Func<int> F = () => 42; }", false)]
    [Arguments("using System; using System.Linq.Expressions; class C { Expression<Func<int>> F = () => 42; }", true)]
    [Arguments("using System.Linq; class C { object M(int[] items) => from item in items select 42; }", true)]
    [Arguments("class C { object F = () => 42; }", false)]
    [Arguments("class C { void M() { var f = (Missing x) => 42; } }", false)]
    [Arguments("class C { void M() { (int value) => 42; } }", false)]
    public async Task ExpressionTreeAncestryMatchesConversionAsync(string source, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("SpanAncestry", [tree], RuntimeMetadataReferences.Platform);
        var literal = (await tree.GetRootAsync()).DescendantNodes().OfType<LiteralExpressionSyntax>().Single();

        await Assert.That(SpanRewriteGuard.IsInsideExpressionTree(literal, compilation.GetSemanticModel(tree), CancellationToken.None)).IsEqualTo(expected);
    }

    /// <summary>Verifies a detached expression has no enclosing expression tree or member boundary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedExpressionHasNoRestrictedAncestorAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }");
        var compilation = CSharpCompilation.Create("DetachedExpression", [tree], RuntimeMetadataReferences.Platform);
        var expression = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));

        await Assert.That(SpanRewriteGuard.IsInsideExpressionTree(expression, compilation.GetSemanticModel(tree), CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies similarly named delegate types do not acquire expression-tree restrictions.</summary>
    /// <param name="typeNamespace">The namespace containing the custom delegate.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Other")]
    [Arguments("Other.Expressions")]
    [Arguments("Other.Linq.Expressions")]
    [Arguments("Other.System.Linq.Expressions")]
    public async Task ExpressionNamedDelegatesOutsideFrameworkNamespaceAreAllowedAsync(string typeNamespace)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            namespace {{typeNamespace}} { public delegate int Expression(); }
            class C { {{typeNamespace}}.Expression field = () => 42; }
            """);
        var compilation = CSharpCompilation.Create("ExpressionNames", [tree], CoreReferences);
        var literal = (await tree.GetRootAsync()).DescendantNodes().OfType<LiteralExpressionSyntax>().Single();

        await Assert.That(SpanRewriteGuard.IsInsideExpressionTree(literal, compilation.GetSemanticModel(tree), CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies only the framework enum has string-comparison semantics.</summary>
    /// <param name="declaration">The type declaration to bind.</param>
    /// <param name="metadataName">The declared type name.</param>
    /// <param name="expected">Whether the type matches the framework enum shape.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("namespace System { enum StringComparison { Value } }", "System.StringComparison", true)]
    [Arguments("namespace System { class StringComparison { } }", "System.StringComparison", false)]
    [Arguments("namespace System { enum Other { Value } }", "System.Other", false)]
    [Arguments("namespace Other { enum StringComparison { Value } }", "Other.StringComparison", false)]
    [Arguments("namespace Other.System { enum StringComparison { Value } }", "Other.System.StringComparison", false)]
    public async Task StringComparisonRequiresExactEnumNamespaceAsync(string declaration, string metadataName, bool expected)
    {
        var compilation = CSharpCompilation.Create("ComparisonTypes", [CSharpSyntaxTree.ParseText(declaration)], CoreReferences);

        await Assert.That(SpanRewriteGuard.IsStringComparison(compilation.GetTypeByMetadataName(metadataName))).IsEqualTo(expected);
        await Assert.That(SpanRewriteGuard.IsStringComparison(null)).IsFalse();
        await Assert.That(SpanRewriteGuard.IsStringComparison(compilation.CreateArrayTypeSymbol(compilation.GetSpecialType(SpecialType.System_Int32)))).IsFalse();
    }

    /// <summary>Verifies lookup requires an unshadowed type directly in the global System namespace.</summary>
    /// <param name="source">The declarations and imports visible at the method.</param>
    /// <param name="name">The simple name to resolve.</param>
    /// <param name="expected">Whether lookup finds the framework namespace.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System; class C { void M() {} }", "String", true)]
    [Arguments("class C { void M() {} }", "String", false)]
    [Arguments("using System; class String {} class C { void M() {} }", "String", false)]
    [Arguments("using System; class C { void M() {} }", "Missing", false)]
    [Arguments("class C { void M() {} }", "System", false)]
    [Arguments("using Other.System; namespace Other.System { class String {} } class C { void M() {} }", "String", false)]
    public async Task SystemLookupRespectsImportsAndShadowingAsync(string source, string name, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("SpanLookup", [tree], RuntimeMetadataReferences.Platform);
        var method = (await tree.GetRootAsync()).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        await Assert.That(SpanRewriteGuard.ResolvesInSystem(compilation.GetSemanticModel(tree), method.Body!.SpanStart, name)).IsEqualTo(expected);
    }
}
