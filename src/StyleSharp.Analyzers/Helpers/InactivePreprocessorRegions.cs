// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Finds source that the current compilation left out: the text of an <c>#if</c> branch that was not taken.
/// Such text is trivia rather than syntax, so nothing in it binds, and a rule that reasons about what a node
/// contains can only see the branch this configuration compiled.
/// </summary>
internal static class InactivePreprocessorRegions
{
    /// <summary>Returns whether an inactive <c>#if</c> region falls anywhere inside a node.</summary>
    /// <param name="node">The node whose source is inspected.</param>
    /// <returns><see langword="true"/> when the node holds text another configuration compiles.</returns>
    /// <remarks>A node with no directives at all answers from a cached flag, without walking a token.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(SyntaxNode node) =>
        node.ContainsDirectives && ContainsDisabledText(node);

    /// <summary>Scans a node's token trivia, including structured trivia, for inactive source.</summary>
    /// <param name="node">The subtree whose trivia is inspected.</param>
    /// <returns><see langword="true"/> when disabled source text is found.</returns>
    internal static bool ContainsDisabledText(SyntaxNode node)
    {
        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.AsNode() is { } childNode)
            {
                if (ContainsDisabledText(childNode))
                {
                    return true;
                }

                continue;
            }

            var token = child.AsToken();
            if (ContainsDisabledText(token.LeadingTrivia) || ContainsDisabledText(token.TrailingTrivia))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks a trivia list, and the tokens inside its structured trivia, for inactive source.</summary>
    /// <param name="triviaList">The leading or trailing trivia to inspect.</param>
    /// <returns><see langword="true"/> when disabled source text is found at any depth.</returns>
    internal static bool ContainsDisabledText(in SyntaxTriviaList triviaList)
    {
        for (var i = 0; i < triviaList.Count; i++)
        {
            var trivia = triviaList[i];
            if (trivia.IsKind(SyntaxKind.DisabledTextTrivia)
                || (trivia.GetStructure() is { } structure && ContainsDisabledText(structure)))
            {
                return true;
            }
        }

        return false;
    }
}
