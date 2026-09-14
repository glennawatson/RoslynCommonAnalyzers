// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Walks a node's ancestors up to a boundary without materializing the ancestor sequence.</summary>
internal static class SyntaxAncestry
{
    /// <summary>Returns whether a node sits inside a <typeparamref name="TNode"/> that is itself inside the limit.</summary>
    /// <typeparam name="TNode">The ancestor kind to look for.</typeparam>
    /// <param name="node">The node whose ancestors are walked; the node itself is not tested.</param>
    /// <param name="limit">The ancestor at which the walk stops, exclusive.</param>
    /// <returns><see langword="true"/> when a <typeparamref name="TNode"/> lies strictly between <paramref name="node"/> and <paramref name="limit"/>.</returns>
    internal static bool HasAncestorBefore<TNode>(SyntaxNode node, SyntaxNode limit)
        where TNode : SyntaxNode
    {
        for (var current = node.Parent; current is not null && current != limit; current = current.Parent)
        {
            if (current is TNode)
            {
                return true;
            }
        }

        return false;
    }
}
