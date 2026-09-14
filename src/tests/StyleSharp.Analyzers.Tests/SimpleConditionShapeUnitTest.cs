// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the loop-condition shape classification.</summary>
public sealed class SimpleConditionShapeUnitTest
{
    /// <summary>The most names a condition may read in these tests.</summary>
    private const int MaximumVariables = 2;

    /// <summary>Verifies a condition is simple only when it holds names, literals and non-stepping operators within the name budget.</summary>
    /// <param name="condition">The condition source.</param>
    /// <param name="expected">Whether the condition is simple.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("i < n", true)]
    [Arguments("(i + 1) != -n", true)]
    [Arguments("!done", true)]
    [Arguments("i < n && j < m", false)]
    [Arguments("1 < 2", false)]
    [Arguments("i < Count()", false)]
    [Arguments("++i < n", false)]
    [Arguments("i < items.Length", false)]
    public async Task ClassifiesConditionShapeAsync(string condition, bool expected)
    {
        var expression = SyntaxFactory.ParseExpression(condition);

        await Assert.That(SimpleConditionShape.IsSimple(expression, MaximumVariables)).IsEqualTo(expected);
    }
}
