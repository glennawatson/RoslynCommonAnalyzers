// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>A single-node edit computed by a code fix: the reported node and its replacement.</summary>
internal readonly record struct NodeReplacement
{
    /// <summary>Initializes a new instance of the <see cref="NodeReplacement"/> struct.</summary>
    /// <param name="original">The node being replaced.</param>
    /// <param name="replacement">The replacement node.</param>
    public NodeReplacement(SyntaxNode original, SyntaxNode replacement)
        : this(original, replacement, rewriteCurrent: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NodeReplacement"/> struct.</summary>
    /// <param name="original">The node being replaced.</param>
    /// <param name="replacement">The replacement node.</param>
    /// <param name="rewriteCurrent">The optional batch rewrite for the current node.</param>
    public NodeReplacement(SyntaxNode original, SyntaxNode replacement, Func<SyntaxNode, SyntaxNode>? rewriteCurrent)
    {
        Original = original;
        Replacement = replacement;
        RewriteCurrent = rewriteCurrent;
    }

    /// <summary>Gets the node being replaced.</summary>
    public SyntaxNode Original { get; }

    /// <summary>Gets the replacement node for single edits.</summary>
    public SyntaxNode Replacement { get; }

    /// <summary>Gets the optional batch rewrite that receives the current node after nested edits are composed.</summary>
    public Func<SyntaxNode, SyntaxNode>? RewriteCurrent { get; }
}
