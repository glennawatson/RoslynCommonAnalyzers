// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Reads the syntactic shape of an expression without binding it.</summary>
internal static class ExpressionShapes
{
    /// <summary>Returns the expression inside any enclosing parentheses.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost expression that is not parenthesized, or <paramref name="expression"/> itself when it is not.</returns>
    internal static ExpressionSyntax WalkDownParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Returns the pattern inside any enclosing parentheses.</summary>
    /// <param name="pattern">The pattern to unwrap.</param>
    /// <returns>The innermost pattern that is not parenthesized, or <paramref name="pattern"/> itself when it is not.</returns>
    internal static PatternSyntax WalkDownParentheses(PatternSyntax pattern)
    {
        while (pattern is ParenthesizedPatternSyntax parenthesized)
        {
            pattern = parenthesized.Pattern;
        }

        return pattern;
    }

    /// <summary>Returns whether an expression is the <see langword="null"/> literal.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for the <see langword="null"/> literal itself; a parenthesized one is not unwrapped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNullLiteral(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns the operand a binary expression compares with the <see langword="null"/> literal.</summary>
    /// <param name="binary">The binary expression, typically <c>==</c> or <c>!=</c>.</param>
    /// <returns>The operand on the other side of a <see langword="null"/> literal, or <see langword="null"/> when neither side is one.</returns>
    internal static ExpressionSyntax? OperandComparedToNull(BinaryExpressionSyntax binary)
    {
        if (IsNullLiteral(binary.Right))
        {
            return binary.Left;
        }

        return IsNullLiteral(binary.Left) ? binary.Right : null;
    }
}
