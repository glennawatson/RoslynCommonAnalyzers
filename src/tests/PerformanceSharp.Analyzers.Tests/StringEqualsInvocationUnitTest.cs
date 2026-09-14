// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Direct tests for <see cref="StringEqualsInvocation"/>, shared by the PSH1200 and PSH1216 code fixes.</summary>
public class StringEqualsInvocationUnitTest
{
    /// <summary>Verifies the call is written with its operands stripped of trivia and a space after each comma.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WritesAllThreeArgumentsAsync()
    {
        var left = SyntaxFactory.ParseExpression(" a ");
        var right = SyntaxFactory.ParseExpression("b /*c*/");
        var comparison = SyntaxFactory.ParseExpression("System.StringComparison.Ordinal");

        var call = StringEqualsInvocation.Build(left, right, comparison);

        await Assert.That(call.ToFullString()).IsEqualTo("string.Equals(a, b, System.StringComparison.Ordinal)");
    }
}
