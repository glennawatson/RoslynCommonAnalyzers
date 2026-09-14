// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the syntactic LINQ selector recognizers.</summary>
public class LinqCallSyntaxTests
{
    /// <summary>Verifies only a one-parameter lambda whose expression body is that parameter is an identity selector.</summary>
    /// <param name="selector">The selector expression.</param>
    /// <param name="expected">Whether the selector is an identity lambda.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("x => x", true)]
    [Arguments("(x) => x", true)]
    [Arguments("(int x) => x", true)]
    [Arguments("x => y", false)]
    [Arguments("(x) => y", false)]
    [Arguments("(x, y) => x", false)]
    [Arguments("() => x", false)]
    [Arguments("x => x.Name", false)]
    [Arguments("x => { return x; }", false)]
    [Arguments("(x) => { return x; }", false)]
    [Arguments("delegate (int x) { return x; }", false)]
    [Arguments("Selector", false)]
    public async Task IdentityLambdaReturnsItsOwnParameterAsync(string selector, bool expected)
    {
        var expression = SyntaxFactory.ParseExpression(selector);

        await Assert.That(LinqCallSyntax.IsIdentityLambda(expression)).IsEqualTo(expected);
    }
}
