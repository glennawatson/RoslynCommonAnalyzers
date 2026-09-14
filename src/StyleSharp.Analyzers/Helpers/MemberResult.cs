// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads whether an expression is everything a member yields: its expression body, or the value of a
/// <c>return</c>, with any parentheses around it. An expression combined with other state is only part of the result.
/// </summary>
internal static class MemberResult
{
    /// <summary>Returns whether an expression is the entire value a member yields.</summary>
    /// <param name="expression">The expression to place.</param>
    /// <returns><see langword="true"/> when the expression is the member's expression body or a returned expression.</returns>
    internal static bool IsWholeResult(ExpressionSyntax expression)
    {
        SyntaxNode node = expression;
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            node = parenthesized;
        }

        return node.Parent switch
        {
            ArrowExpressionClauseSyntax arrow => arrow.Expression == node,
            ReturnStatementSyntax returnStatement => returnStatement.Expression == node,
            _ => false,
        };
    }
}
