// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>
/// Allocation-free preorder descendant traversal helpers built on indexed
/// <see cref="SyntaxNode.ChildNodesAndTokens()"/> scans rather than Roslyn's
/// iterator-based descendant enumerators.
/// </summary>
/// <remarks>
/// Visitors receive the caller's state by reference and return <see langword="true"/> to continue or
/// <see langword="false"/> to stop. Pass a static method group or a static lambda so no delegate is allocated per call.
/// </remarks>
internal static class DescendantTraversalHelper
{
    /// <summary>
    /// Visits every token beneath <paramref name="root"/> in document (preorder)
    /// order, mirroring <see cref="SyntaxNode.DescendantTokens(Func{SyntaxNode, bool}, bool)"/>
    /// but without allocating an iterator or its internal stack. The visitor may
    /// stop the walk early by returning <see langword="false"/>.
    /// </summary>
    /// <typeparam name="TState">The caller state threaded through the traversal.</typeparam>
    /// <param name="root">The root whose descendant tokens to visit.</param>
    /// <param name="state">The caller state.</param>
    /// <param name="visitor">Returns <see langword="true"/> to continue, or <see langword="false"/> to stop.</param>
    /// <returns><see langword="true"/> when the full traversal completed.</returns>
    internal static bool VisitDescendantTokens<TState>(SyntaxNode root, ref TState state, FuncInRef<SyntaxToken, TState, bool> visitor)
    {
        var children = root.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.IsToken)
            {
                if (!visitor(child.AsToken(), ref state))
                {
                    return false;
                }
            }
            else if (child.AsNode() is { } childNode && !VisitDescendantTokens(childNode, ref state, visitor))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Visits every token beneath a root in document order until the visitor asks to stop.</summary>
    /// <param name="root">The root whose descendant tokens to visit.</param>
    /// <param name="visitor">Returns <see langword="true"/> to continue, or <see langword="false"/> to stop.</param>
    /// <returns><see langword="true"/> when the full traversal completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool VisitDescendantTokens(SyntaxNode root, Func<SyntaxToken, bool> visitor) =>
        VisitDescendantTokens(root, ref visitor, InvokeTokenVisitor);

    /// <summary>Visits matching descendants in preorder until the visitor asks to stop.</summary>
    /// <typeparam name="TNode">The descendant node type to surface to the visitor.</typeparam>
    /// <typeparam name="TState">The caller state threaded through the traversal.</typeparam>
    /// <param name="root">The root whose descendants to visit.</param>
    /// <param name="state">The caller state.</param>
    /// <param name="visitor">Returns <see langword="true"/> to continue, or <see langword="false"/> to stop.</param>
    /// <returns><see langword="true"/> when the full traversal completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool VisitDescendants<TNode, TState>(SyntaxNode root, ref TState state, FuncRef<TNode, TState, bool> visitor)
        where TNode : SyntaxNode =>
        VisitChildren(root, ref state, visitor);

    /// <summary>Visits matching descendants in preorder until the visitor asks to stop.</summary>
    /// <typeparam name="TNode">The descendant node type to surface to the visitor.</typeparam>
    /// <param name="root">The root whose descendants to visit.</param>
    /// <param name="visitor">Returns <see langword="true"/> to continue, or <see langword="false"/> to stop.</param>
    /// <returns><see langword="true"/> when the full traversal completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool VisitDescendants<TNode>(SyntaxNode root, Func<TNode, bool> visitor)
        where TNode : SyntaxNode =>
        VisitChildren<TNode, Func<TNode, bool>>(root, ref visitor, InvokeVisitor);

    /// <summary>Visits one node, then its descendants, in preorder.</summary>
    /// <typeparam name="TNode">The descendant node type to surface to the visitor.</typeparam>
    /// <typeparam name="TState">The caller state threaded through the traversal.</typeparam>
    /// <param name="node">The current node.</param>
    /// <param name="state">The caller state.</param>
    /// <param name="visitor">Returns <see langword="true"/> to continue, or <see langword="false"/> to stop.</param>
    /// <returns><see langword="true"/> when the subtree traversal completed.</returns>
    private static bool Visit<TNode, TState>(SyntaxNode node, ref TState state, FuncRef<TNode, TState, bool> visitor)
        where TNode : SyntaxNode =>
        (node is not TNode match || visitor(match, ref state)) && VisitChildren(node, ref state, visitor);

    /// <summary>Visits a node's child nodes, and each one's descendants, in preorder.</summary>
    /// <typeparam name="TNode">The descendant node type to surface to the visitor.</typeparam>
    /// <typeparam name="TState">The caller state threaded through the traversal.</typeparam>
    /// <param name="node">The node whose children to visit; the node itself is not visited.</param>
    /// <param name="state">The caller state.</param>
    /// <param name="visitor">Returns <see langword="true"/> to continue, or <see langword="false"/> to stop.</param>
    /// <returns><see langword="true"/> when every child subtree completed.</returns>
    private static bool VisitChildren<TNode, TState>(SyntaxNode node, ref TState state, FuncRef<TNode, TState, bool> visitor)
        where TNode : SyntaxNode
    {
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

    /// <summary>Hands a token to a visitor that carries no state.</summary>
    /// <param name="token">The visited token.</param>
    /// <param name="visitor">The stateless visitor.</param>
    /// <returns>The visitor's result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InvokeTokenVisitor(in SyntaxToken token, ref Func<SyntaxToken, bool> visitor) => visitor(token);

    /// <summary>Hands a node to a visitor that carries no state.</summary>
    /// <typeparam name="TNode">The visited node type.</typeparam>
    /// <param name="node">The visited node.</param>
    /// <param name="visitor">The stateless visitor.</param>
    /// <returns>The visitor's result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InvokeVisitor<TNode>(TNode node, ref Func<TNode, bool> visitor)
        where TNode : SyntaxNode =>
        visitor(node);
}
