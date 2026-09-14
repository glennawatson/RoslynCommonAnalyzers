// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Wraps an expression in parentheses that take over its surrounding trivia.</summary>
internal static class ExpressionParentheses
{
    /// <summary>Wraps an expression in parentheses.</summary>
    /// <param name="expression">The expression to wrap.</param>
    /// <returns>
    /// The parenthesized expression. The expression's leading trivia moves before the opening parenthesis and its
    /// trailing trivia after the closing one, and the space inside the parentheses is left for the formatter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ParenthesizedExpressionSyntax Wrap(ExpressionSyntax expression) =>
        SyntaxFactory.ParenthesizedExpression(
            SyntaxFactory.Token(expression.GetLeadingTrivia(), SyntaxKind.OpenParenToken, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
            expression.WithoutTrivia(),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, expression.GetTrailingTrivia()));
}
