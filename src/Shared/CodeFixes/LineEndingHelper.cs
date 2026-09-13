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
    internal static SyntaxTrivia GetLineBreak(SyntaxNode anchor)
    {
        var lineBreak = default(SyntaxTrivia);
        _ = DescendantTraversalHelper.VisitDescendantTokens(
            anchor,
            ref lineBreak,
            static (in SyntaxToken token, ref SyntaxTrivia found) =>
            {
                foreach (var trivia in token.LeadingTrivia)
                {
                    if (!trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        continue;
                    }

                    found = trivia;
                    return false;
                }

                foreach (var trivia in token.TrailingTrivia)
                {
                    if (!trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        continue;
                    }

                    found = trivia;
                    return false;
                }

                return true;
            });

        return lineBreak.RawKind == 0 ? SyntaxFactory.EndOfLine("\n") : lineBreak;
    }
}
