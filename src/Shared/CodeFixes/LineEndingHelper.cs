// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>
/// Resolves the line-break trivia a code fix should insert: the one the edited file already
/// uses, so generated lines match CRLF and LF sources alike instead of hard-coding one form.
/// </summary>
internal static class LineEndingHelper
{
    /// <summary>Returns the anchor's own line-break trivia, falling back to a bare line feed.</summary>
    /// <param name="anchor">The node whose file supplies the convention.</param>
    /// <returns>The end-of-line trivia to insert.</returns>
    public static SyntaxTrivia GetLineBreak(SyntaxNode anchor)
    {
        // A trivia walk is fine here: fixes run on demand for one reported node, never on the
        // analyzer hot path, and the first line break almost always sits within a few tokens.
        foreach (var trivia in anchor.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return trivia;
            }
        }

        return SyntaxFactory.EndOfLine("\n");
    }
}
