// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Syntax-only probes for plain single-character string literals — the argument shape the
/// char-overload rules (PSH1201, PSH1202) accept. Verbatim, raw, interpolated, and UTF-8
/// literals are rejected so a fix can always substitute an equivalent char literal.
/// </summary>
internal static class StringLiteralHelper
{
    /// <summary>Returns whether an expression is a plain one-character string literal.</summary>
    /// <param name="expression">The candidate argument expression.</param>
    /// <param name="literal">The matched literal when the probe succeeds.</param>
    /// <param name="value">The literal's single character.</param>
    /// <returns><see langword="true"/> for a regular <c>"x"</c> literal whose value is exactly one character.</returns>
    public static bool TryGetSingleCharacterLiteral(ExpressionSyntax expression, out LiteralExpressionSyntax? literal, out char value)
    {
        if (expression is LiteralExpressionSyntax candidate
            && candidate.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var token = candidate.Token;
            if (token.IsKind(SyntaxKind.StringLiteralToken)
                && token.Text.Length > 0
                && token.Text[0] == '"'
                && token.ValueText.Length == 1)
            {
                literal = candidate;
                value = token.ValueText[0];
                return true;
            }
        }

        literal = null;
        value = default;
        return false;
    }
}
