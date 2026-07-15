// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Wraps a replacement expression in an <c>await</c>, shared by the fixes that turn a synchronous
/// call into an awaited one (PSH1313, PSH1315). The result is parenthesized wherever the
/// surrounding expression binds tighter than <c>await</c> — <c>task.Result.Length</c> becomes
/// <c>(await task).Length</c>, not <c>await task.Length</c> — and carries the replaced expression's
/// trivia plus a formatter annotation.
/// </summary>
internal static class AwaitExpressionRewrite
{
    /// <summary>Wraps an expression in an <c>await</c>, parenthesized where the surrounding expression needs it.</summary>
    /// <param name="awaited">The expression to await.</param>
    /// <param name="original">The expression being replaced, whose trivia and position the result takes on.</param>
    /// <returns>The awaited expression.</returns>
    public static ExpressionSyntax WrapInAwait(ExpressionSyntax awaited, ExpressionSyntax original)
    {
        ExpressionSyntax result = SyntaxFactory.AwaitExpression(
            SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            awaited.WithoutTrivia());
        if (NeedsParenthesesAfterAwait(original))
        {
            result = SyntaxFactory.ParenthesizedExpression(result);
        }

        return result.WithTriviaFrom(original).WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Returns whether the surrounding expression binds tighter than <c>await</c>, so the result needs parentheses.</summary>
    /// <param name="expression">The expression being replaced.</param>
    /// <returns><see langword="true"/> when the replacement must be parenthesized to keep its meaning.</returns>
    public static bool NeedsParenthesesAfterAwait(ExpressionSyntax expression)
        => expression.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Expression == expression,
            ElementAccessExpressionSyntax element => element.Expression == expression,
            InvocationExpressionSyntax invocation => invocation.Expression == expression,
            ConditionalAccessExpressionSyntax conditional => conditional.Expression == expression,
            PostfixUnaryExpressionSyntax => true,
            _ => false,
        };
}
