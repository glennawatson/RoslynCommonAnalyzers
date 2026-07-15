// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Direct tests for <see cref="LinqCallSyntax"/>, the syntactic recognizers shared between the
/// Collections analyzers and their code fixes.
/// </summary>
public class LinqCallSyntaxUnitTest
{
    /// <summary>Verifies a single one-parameter lambda argument is accepted in both lambda shapes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetOneParameterLambdaAcceptsSimpleAndParenthesizedSingleParameterAsync()
    {
        await Assert.That(LinqCallSyntax.TryGetOneParameterLambda(Invocation("xs.Where(x => x > 0)"), out var simple)).IsTrue();
        await Assert.That(simple is SimpleLambdaExpressionSyntax).IsTrue();

        await Assert.That(LinqCallSyntax.TryGetOneParameterLambda(Invocation("xs.Where((x) => x > 0)"), out var parenthesized)).IsTrue();
        await Assert.That(parenthesized is ParenthesizedLambdaExpressionSyntax).IsTrue();
    }

    /// <summary>Verifies wrong argument counts and multi-parameter lambdas are rejected.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetOneParameterLambdaRejectsNonSingleParameterShapesAsync()
    {
        await Assert.That(LinqCallSyntax.TryGetOneParameterLambda(Invocation("xs.ToList()"), out _)).IsFalse();
        await Assert.That(LinqCallSyntax.TryGetOneParameterLambda(Invocation("xs.OrderBy(x => x, cmp)"), out _)).IsFalse();
        await Assert.That(LinqCallSyntax.TryGetOneParameterLambda(Invocation("xs.Zip(ys, (a, b) => a)"), out _)).IsFalse();
    }

    /// <summary>Verifies the predicate lambda's parameter name and body are surfaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetPredicateLambdaSurfacesParameterNameAndBodyAsync()
    {
        await Assert.That(LinqCallSyntax.TryGetPredicateLambda(Expression("item => item.Value == 3"), out var name, out var body)).IsTrue();
        await Assert.That(name).IsEqualTo("item");
        await Assert.That(body!.ToString()).IsEqualTo("item.Value == 3");

        await Assert.That(LinqCallSyntax.TryGetPredicateLambda(Expression("SomeMethod"), out _, out _)).IsFalse();
        await Assert.That(LinqCallSyntax.TryGetPredicateLambda(Expression("() => 3"), out _, out _)).IsFalse();
    }

    /// <summary>Verifies the compared value is taken from whichever side is not the parameter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetComparedValueReadsTheNonParameterSideAsync()
    {
        await Assert.That(LinqCallSyntax.TryGetComparedValue(Equality("x == 5"), "x", out var right)).IsTrue();
        await Assert.That(right.ToString()).IsEqualTo("5");

        await Assert.That(LinqCallSyntax.TryGetComparedValue(Equality("value == x"), "x", out var left)).IsTrue();
        await Assert.That(left.ToString()).IsEqualTo("value");

        await Assert.That(LinqCallSyntax.TryGetComparedValue(Equality("a == b"), "x", out _)).IsFalse();
    }

    /// <summary>Verifies only the four ordering operators are treated as sorts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsSortMethodNameMatchesOnlyOrderingOperatorsAsync()
    {
        await Assert.That(LinqCallSyntax.IsSortMethodName("OrderBy")).IsTrue();
        await Assert.That(LinqCallSyntax.IsSortMethodName("OrderByDescending")).IsTrue();
        await Assert.That(LinqCallSyntax.IsSortMethodName("ThenBy")).IsTrue();
        await Assert.That(LinqCallSyntax.IsSortMethodName("ThenByDescending")).IsTrue();
        await Assert.That(LinqCallSyntax.IsSortMethodName("Where")).IsFalse();
        await Assert.That(LinqCallSyntax.IsSortMethodName("Select")).IsFalse();
    }

    /// <summary>Parses an invocation expression.</summary>
    /// <param name="text">The expression source.</param>
    /// <returns>The parsed invocation.</returns>
    private static InvocationExpressionSyntax Invocation(string text)
        => (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(text);

    /// <summary>Parses an expression fragment.</summary>
    /// <param name="text">The expression source.</param>
    /// <returns>The parsed expression.</returns>
    private static ExpressionSyntax Expression(string text)
        => SyntaxFactory.ParseExpression(text);

    /// <summary>Parses a binary equality expression.</summary>
    /// <param name="text">The expression source.</param>
    /// <returns>The parsed binary expression.</returns>
    private static BinaryExpressionSyntax Equality(string text)
        => (BinaryExpressionSyntax)SyntaxFactory.ParseExpression(text);
}
