// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared syntactic reads of an expression's shape.</summary>
public sealed class ExpressionShapesUnitTest
{
    /// <summary>Verifies nested parentheses are all removed to reach the inner expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WalkDownParenthesesRemovesEveryEnclosingPairAsync()
    {
        var expression = SyntaxFactory.ParseExpression("((value))");

        var inner = ExpressionShapes.WalkDownParentheses(expression);

        await Assert.That(inner).IsTypeOf<IdentifierNameSyntax>();
        await Assert.That(inner.ToString()).IsEqualTo("value");
    }

    /// <summary>Verifies an expression without parentheses comes back as the same node.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WalkDownParenthesesLeavesAnUnwrappedExpressionAsync()
    {
        var expression = SyntaxFactory.ParseExpression("value + 1");

        await Assert.That(ExpressionShapes.WalkDownParentheses(expression)).IsSameReferenceAs(expression);
    }

    /// <summary>Verifies nested pattern parentheses are all removed and an unwrapped pattern comes back as the same node.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WalkDownParenthesesRemovesEveryEnclosingPatternPairAsync()
    {
        var wrapped = ((IsPatternExpressionSyntax)SyntaxFactory.ParseExpression("value is ((not null))")).Pattern;
        var bare = ((IsPatternExpressionSyntax)SyntaxFactory.ParseExpression("value is not null")).Pattern;

        var inner = ExpressionShapes.WalkDownParentheses(wrapped);

        await Assert.That(inner).IsTypeOf<UnaryPatternSyntax>();
        await Assert.That(inner.ToString()).IsEqualTo("not null");
        await Assert.That(ExpressionShapes.WalkDownParentheses(bare)).IsSameReferenceAs(bare);
    }

    /// <summary>Verifies only the bare <see langword="null"/> literal matches.</summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">Whether it is the null literal.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("null", true)]
    [Arguments("(null)", false)]
    [Arguments("default", false)]
    [Arguments("value", false)]
    public async Task IsNullLiteralMatchesOnlyTheLiteralAsync(string text, bool expected) =>
        await Assert.That(ExpressionShapes.IsNullLiteral(SyntaxFactory.ParseExpression(text))).IsEqualTo(expected);

    /// <summary>Verifies the operand across from a <see langword="null"/> literal is found on either side.</summary>
    /// <param name="text">The comparison text.</param>
    /// <param name="expected">The expected operand text, or <see langword="null"/> when neither side is the literal.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("value == null", "value")]
    [Arguments("null != value", "value")]
    [Arguments("value == other", null)]
    public async Task OperandComparedToNullReadsEitherSideAsync(string text, string? expected) =>
        await Assert.That(ExpressionShapes.OperandComparedToNull((BinaryExpressionSyntax)SyntaxFactory.ParseExpression(text))?.ToString()).IsEqualTo(expected);
}
