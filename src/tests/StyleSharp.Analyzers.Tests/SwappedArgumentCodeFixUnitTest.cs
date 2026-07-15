// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared two-argument swap used by the transposed-argument code fixes.</summary>
public sealed class SwappedArgumentCodeFixUnitTest
{
    /// <summary>An existing descriptor, reused only to build a diagnostic that carries the swap position.</summary>
    private static readonly DiagnosticDescriptor TestRule = CorrectnessRules.SwappedArguments;

    /// <summary>Verifies the swap exchanges the two expressions and keeps each argument's trivia.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwapExchangesTheTwoArgumentExpressionsAsync()
    {
        var list = ParseFirstArgumentList("class C { void M() { N(a, b); } }");

        var swapped = SwappedArgumentCodeFix.Swap(list, 0, 1);

        await Assert.That(swapped.ToString()).IsEqualTo("(b, a)");
    }

    /// <summary>Verifies out-of-range and equal positions are rejected while a real pair is accepted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsSwappablePairAcceptsDistinctInRangePositionsAsync()
    {
        var list = ParseFirstArgumentList("class C { void M() { N(a, b); } }");

        await Assert.That(SwappedArgumentCodeFix.IsSwappablePair(list, 0, 1)).IsTrue();
        await Assert.That(SwappedArgumentCodeFix.IsSwappablePair(list, 0, 0)).IsFalse();
        await Assert.That(SwappedArgumentCodeFix.IsSwappablePair(list, 0, 5)).IsFalse();
        await Assert.That(SwappedArgumentCodeFix.IsSwappablePair(list, -1, 1)).IsFalse();
    }

    /// <summary>Verifies the diagnostic-driven build resolves the reported argument and swaps it into place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryBuildSwapReordersUsingThePropertyPosition()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C { void M() { N(a, b); } }");
        var list = FirstArgumentList(root);
        var properties = ImmutableDictionary<string, string?>.Empty.Add("SwapWith", "1");
        var diagnostic = Diagnostic.Create(TestRule, list.Arguments[0].GetLocation(), properties);

        var edit = SwappedArgumentCodeFix.TryBuildSwap(root, diagnostic, "SwapWith");

        await Assert.That(edit.HasValue).IsTrue();
        await Assert.That(edit!.Value.Replacement.ToString()).IsEqualTo("(b, a)");
    }

    /// <summary>Verifies a missing swap-position property yields no edit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryBuildSwapReturnsNullWhenThePropertyIsAbsent()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C { void M() { N(a, b); } }");
        var list = FirstArgumentList(root);
        var diagnostic = Diagnostic.Create(TestRule, list.Arguments[0].GetLocation());

        var edit = SwappedArgumentCodeFix.TryBuildSwap(root, diagnostic, "SwapWith");

        await Assert.That(edit.HasValue).IsFalse();
    }

    /// <summary>Parses the first argument list from a single-type snippet, detached from a tree.</summary>
    /// <param name="source">The source snippet.</param>
    /// <returns>The first argument list.</returns>
    private static ArgumentListSyntax ParseFirstArgumentList(string source)
        => FirstArgumentList(SyntaxFactory.ParseCompilationUnit(source));

    /// <summary>Gets the first argument list under a compilation unit root.</summary>
    /// <param name="root">The compilation unit root.</param>
    /// <returns>The first argument list.</returns>
    private static ArgumentListSyntax FirstArgumentList(CompilationUnitSyntax root)
    {
        var method = (MethodDeclarationSyntax)((TypeDeclarationSyntax)root.Members[0]).Members[0];
        var invocation = (InvocationExpressionSyntax)((ExpressionStatementSyntax)method.Body!.Statements[0]).Expression;
        return invocation.ArgumentList;
    }
}
