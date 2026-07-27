// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>A single-node deletion computed by a code fix: the node to drop and how to treat its trivia.</summary>
internal readonly record struct NodeRemoval
{
    /// <summary>Initializes a new instance of the <see cref="NodeRemoval"/> struct.</summary>
    /// <param name="node">The node to remove.</param>
    /// <remarks>Trivia goes with the node, which is what dropping a whole statement or member wants.</remarks>
    public NodeRemoval(SyntaxNode node)
        : this(node, SyntaxRemoveOptions.KeepNoTrivia)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NodeRemoval"/> struct.</summary>
    /// <param name="node">The node to remove.</param>
    /// <param name="options">How the removal treats the node's trivia and directives.</param>
    public NodeRemoval(SyntaxNode node, SyntaxRemoveOptions options)
    {
        Node = node;
        Options = options;
    }

    /// <summary>Gets the node being removed.</summary>
    public SyntaxNode Node { get; }

    /// <summary>Gets the options controlling what survives the removal.</summary>
    public SyntaxRemoveOptions Options { get; }

    /// <summary>Creates a removal that leaves a leading comment banner and unbalanced directives behind.</summary>
    /// <param name="node">The node to remove.</param>
    /// <returns>The removal.</returns>
    /// <remarks>
    /// Deleting a using directive or a case label should not take the licence header or the <c>#if</c> that
    /// happens to sit above it, so leading trivia is kept only when it carries something other than layout.
    /// </remarks>
    public static NodeRemoval PreservingLeadingContent(SyntaxNode node)
    {
        var options = SyntaxRemoveOptions.KeepUnbalancedDirectives;
        if (HasSignificantLeadingTrivia(node))
        {
            options |= SyntaxRemoveOptions.KeepLeadingTrivia;
        }

        return new NodeRemoval(node, options);
    }

    /// <summary>Returns whether a node's leading trivia carries content worth keeping.</summary>
    /// <param name="node">The node being removed.</param>
    /// <returns><see langword="true"/> when anything other than whitespace leads the node.</returns>
    private static bool HasSignificantLeadingTrivia(SyntaxNode node)
    {
        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }
}
