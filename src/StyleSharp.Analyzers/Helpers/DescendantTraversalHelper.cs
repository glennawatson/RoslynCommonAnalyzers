// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Allocation-free preorder descendant traversal helpers built on indexed
/// <see cref="SyntaxNode.ChildNodesAndTokens()"/> scans rather than Roslyn's
/// iterator-based descendant enumerators.
/// </summary>
internal static class DescendantTraversalHelper
{
    /// <summary>Represents a preorder descendant visitor.</summary>
    /// <typeparam name="TNode">The descendant node type to surface to the visitor.</typeparam>
    /// <typeparam name="TState">The caller state threaded through the traversal.</typeparam>
    /// <param name="node">The current matching descendant.</param>
    /// <param name="state">The caller state.</param>
    /// <returns><see langword="true"/> to continue, or <see langword="false"/> to stop.</returns>
    public delegate bool DescendantVisitor<in TNode, TState>(TNode node, ref TState state)
        where TNode : SyntaxNode;

    /// <summary>Visits matching descendants in preorder until the visitor asks to stop.</summary>
    /// <typeparam name="TNode">The descendant node type to surface to the visitor.</typeparam>
    /// <typeparam name="TState">The caller state threaded through the traversal.</typeparam>
    /// <param name="root">The root whose descendants to visit.</param>
    /// <param name="state">The caller state.</param>
    /// <param name="visitor">Returns <see langword="true"/> to continue, or <see langword="false"/> to stop.</param>
    /// <returns><see langword="true"/> when the full traversal completed.</returns>
    public static bool VisitDescendants<TNode, TState>(SyntaxNode root, ref TState state, DescendantVisitor<TNode, TState> visitor)
        where TNode : SyntaxNode
    {
        var children = root.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsNode || child.AsNode() is not { } childNode)
            {
                continue;
            }

            if (!Visit(childNode, ref state, visitor))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Visits one node, then its descendants, in preorder.</summary>
    /// <typeparam name="TNode">The descendant node type to surface to the visitor.</typeparam>
    /// <typeparam name="TState">The caller state threaded through the traversal.</typeparam>
    /// <param name="node">The current node.</param>
    /// <param name="state">The caller state.</param>
    /// <param name="visitor">Returns <see langword="true"/> to continue, or <see langword="false"/> to stop.</param>
    /// <returns><see langword="true"/> when the subtree traversal completed.</returns>
    private static bool Visit<TNode, TState>(SyntaxNode node, ref TState state, DescendantVisitor<TNode, TState> visitor)
        where TNode : SyntaxNode
    {
        if (node is TNode match && !visitor(match, ref state))
        {
            return false;
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsNode || child.AsNode() is not { } childNode)
            {
                continue;
            }

            if (!Visit(childNode, ref state, visitor))
            {
                return false;
            }
        }

        return true;
    }
}
