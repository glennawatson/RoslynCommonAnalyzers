// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the trivia around a node for anything other than spaces and line breaks. A rewrite that folds a node
/// into its neighbour has nowhere to carry a comment or directive that sits beside it, so such a node is left alone.
/// </summary>
internal static class SurroundingTrivia
{
    /// <summary>Returns whether a node's leading or trailing trivia holds something other than whitespace.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when a comment, directive or other non-whitespace trivia is present.</returns>
    internal static bool HasNonWhitespace(SyntaxNode node)
    {
        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        foreach (var trivia in node.GetTrailingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }
}
