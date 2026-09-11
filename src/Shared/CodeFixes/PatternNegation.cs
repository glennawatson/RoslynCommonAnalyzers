// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>Flips a pattern between its plain and <c>not</c> forms.</summary>
internal static class PatternNegation
{
    /// <summary>Negates a pattern, dropping an existing <c>not</c> rather than stacking a second one.</summary>
    /// <param name="pattern">The pattern to negate.</param>
    /// <returns>The negated pattern.</returns>
    /// <remarks>
    /// <c>not</c> binds tighter than <c>or</c> and <c>and</c>, so negating a combined pattern needs a group
    /// around it: <c>is not 'a' or 'b'</c> reads as <c>(not 'a') or 'b'</c>, which matches something else
    /// entirely and leaves the later patterns unreachable.
    /// </remarks>
    internal static PatternSyntax Negate(PatternSyntax pattern) =>
        pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } negated
            ? Ungroup(negated.Pattern)
            : SyntaxFactory.UnaryPattern(
                SyntaxFactory.Token(default, SyntaxKind.NotKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
                Group(pattern));

    /// <summary>Wraps a pattern that binds looser than <c>not</c>.</summary>
    /// <param name="pattern">The pattern going under the <c>not</c>.</param>
    /// <returns>The pattern, grouped when it combines others.</returns>
    private static PatternSyntax Group(PatternSyntax pattern) =>
        pattern is BinaryPatternSyntax ? SyntaxFactory.ParenthesizedPattern(pattern) : pattern;

    /// <summary>Removes a group that is no longer needed once the <c>not</c> is gone.</summary>
    /// <param name="pattern">The pattern that was under the <c>not</c>.</param>
    /// <returns>The pattern without its redundant group.</returns>
    private static PatternSyntax Ungroup(PatternSyntax pattern) =>
        pattern is ParenthesizedPatternSyntax { Pattern: BinaryPatternSyntax combined } ? combined : pattern;
}
