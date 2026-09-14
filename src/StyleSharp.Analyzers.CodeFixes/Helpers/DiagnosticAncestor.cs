// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Finds the node a diagnostic was reported inside by walking up from the token at the start of its span.</summary>
internal static class DiagnosticAncestor
{
    /// <summary>Finds the nearest node of one type that encloses the token at the start of a span.</summary>
    /// <typeparam name="T">The node type to find.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic's source span.</param>
    /// <returns>The nearest enclosing node of that type, or <see langword="null"/> when there is none.</returns>
    internal static T? Find<T>(SyntaxNode root, TextSpan span)
        where T : SyntaxNode
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is T matched)
            {
                return matched;
            }

            node = node.Parent;
        }

        return null;
    }
}
