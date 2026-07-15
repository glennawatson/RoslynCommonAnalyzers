// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Direct tests for <see cref="AwaitExpressionRewrite"/>, the await-wrapping shared by the
/// PSH1313 and PSH1315 code fixes.
/// </summary>
public class AwaitExpressionRewriteUnitTest
{
    /// <summary>Verifies an expression whose parent binds tighter than <c>await</c> is flagged for parentheses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NeedsParenthesesAfterAwaitFlagsTightlyBoundReceiversAsync()
    {
        await Assert.That(AwaitExpressionRewrite.NeedsParenthesesAfterAwait(MemberReceiverOf("task.Result"))).IsTrue();
        await Assert.That(AwaitExpressionRewrite.NeedsParenthesesAfterAwait(ElementReceiverOf("task[0]"))).IsTrue();
        await Assert.That(AwaitExpressionRewrite.NeedsParenthesesAfterAwait(CalleeOf("task()"))).IsTrue();
    }

    /// <summary>Verifies a loosely bound context does not force parentheses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NeedsParenthesesAfterAwaitLeavesLooseContextsAloneAsync()
    {
        var addition = (BinaryExpressionSyntax)SyntaxFactory.ParseExpression("task + other");
        await Assert.That(AwaitExpressionRewrite.NeedsParenthesesAfterAwait(addition.Left)).IsFalse();
        await Assert.That(AwaitExpressionRewrite.NeedsParenthesesAfterAwait(SyntaxFactory.ParseExpression("task"))).IsFalse();
    }

    /// <summary>Verifies a standalone expression becomes a bare awaited expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrapInAwaitBuildsABareAwaitForLooseContextsAsync()
    {
        var expression = SyntaxFactory.ParseExpression("task");

        var awaited = AwaitExpressionRewrite.WrapInAwait(expression, expression);

        await Assert.That(awaited is AwaitExpressionSyntax).IsTrue();
        await Assert.That(awaited.ToString()).IsEqualTo("await task");
    }

    /// <summary>Verifies a tightly bound context wraps the await in parentheses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrapInAwaitParenthesizesTightlyBoundContextsAsync()
    {
        var receiver = MemberReceiverOf("task.Result");

        var awaited = AwaitExpressionRewrite.WrapInAwait(receiver, receiver);

        await Assert.That(awaited is ParenthesizedExpressionSyntax).IsTrue();
        await Assert.That(awaited.ToString()).IsEqualTo("(await task)");
    }

    /// <summary>Parses a member access and returns the receiver being accessed.</summary>
    /// <param name="text">The member-access source.</param>
    /// <returns>The receiver expression.</returns>
    private static ExpressionSyntax MemberReceiverOf(string text)
        => ((MemberAccessExpressionSyntax)SyntaxFactory.ParseExpression(text)).Expression;

    /// <summary>Parses an element access and returns the indexed expression.</summary>
    /// <param name="text">The element-access source.</param>
    /// <returns>The indexed expression.</returns>
    private static ExpressionSyntax ElementReceiverOf(string text)
        => ((ElementAccessExpressionSyntax)SyntaxFactory.ParseExpression(text)).Expression;

    /// <summary>Parses an invocation and returns the callee expression.</summary>
    /// <param name="text">The invocation source.</param>
    /// <returns>The callee expression.</returns>
    private static ExpressionSyntax CalleeOf(string text)
        => ((InvocationExpressionSyntax)SyntaxFactory.ParseExpression(text)).Expression;
}
