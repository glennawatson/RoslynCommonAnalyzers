// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the syntax gates for a call made through a plain member access.</summary>
public class SimpleMemberCallTests
{
    /// <summary>The member name every gate is asked about.</summary>
    private const string MemberName = "IndexOf";

    /// <summary>Verifies only a simple member access with the name matches.</summary>
    /// <param name="call">The invocation text.</param>
    /// <param name="expected">Whether the call is named through a simple member access.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("text.IndexOf('a')", true)]
    [Arguments("text.IndexOf()", true)]
    [Arguments("a.b.IndexOf(1)", true)]
    [Arguments("text.LastIndexOf('a')", false)]
    [Arguments("IndexOf('a')", false)]
    [Arguments("text?.IndexOf('a')", false)]
    [Arguments("pointer->IndexOf('a')", false)]
    public async Task CallIsNamedThroughASimpleMemberAccessAsync(string call, bool expected)
    {
        var invocation = SyntaxFactory.ParseExpression(call).DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().First();

        await Assert.That(SimpleMemberCall.IsNamed(invocation, MemberName)).IsEqualTo(expected);
    }

    /// <summary>Verifies the argument-taking gate also requires an invocation with at least one argument.</summary>
    /// <param name="expression">The expression text.</param>
    /// <param name="expected">Whether the expression is a named simple member call with arguments.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("text.IndexOf('a')", true)]
    [Arguments("text.IndexOf()", false)]
    [Arguments("text.IndexOf", false)]
    [Arguments("text.LastIndexOf('a')", false)]
    [Arguments("IndexOf('a')", false)]
    public async Task ArgumentTakingCallNeedsAnArgumentAsync(string expression, bool expected) =>
        await Assert.That(SimpleMemberCall.IsNamedWithArguments(SyntaxFactory.ParseExpression(expression), MemberName)).IsEqualTo(expected);
}
