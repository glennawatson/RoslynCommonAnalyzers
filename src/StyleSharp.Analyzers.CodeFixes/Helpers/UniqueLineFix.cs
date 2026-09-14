// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// The find-and-rewrite plumbing every entries-on-unique-lines code fix shares: it resolves the reported node of one syntax
/// type and rewrites it with the provider's list split, for the lightbulb, the applied action and fix-all alike.
/// </summary>
/// <typeparam name="TNode">The reported node type.</typeparam>
/// <remarks>
/// A provider holds one instance built from its rewrite, so the delegates the shared code-fix helpers take are created once
/// per provider rather than once per diagnostic.
/// </remarks>
internal sealed class UniqueLineFix<TNode>
    where TNode : SyntaxNode
{
    /// <summary>Splits the node's list so each entry sits on its own line.</summary>
    private readonly Func<TNode, TNode> _rewrite;

    /// <summary>Resolves the reported node from a diagnostic.</summary>
    private readonly Func<SyntaxNode, Diagnostic, TNode?> _resolve;

    /// <summary>Re-applies the split to the node as it stands after earlier batch edits.</summary>
    private readonly Func<SyntaxNode, SyntaxNode> _rewriteCurrent;

    /// <summary>Initializes a new instance of the <see cref="UniqueLineFix{TNode}"/> class.</summary>
    /// <param name="rewrite">Splits the node's list so each entry sits on its own line.</param>
    /// <param name="resolve">Resolves the reported node when it is not simply the node at the diagnostic's span; null for that default.</param>
    internal UniqueLineFix(Func<TNode, TNode> rewrite, Func<SyntaxNode, Diagnostic, TNode?>? resolve = null)
    {
        _rewrite = rewrite;
        _resolve = resolve ?? FindReportedNode;
        _rewriteCurrent = current => rewrite((TNode)current);
        CanRewrite = resolve is null
            ? static (root, diagnostic) => root.FindNode(diagnostic.Location.SourceSpan) is TNode
            : (root, diagnostic) => resolve(root, diagnostic) is not null;
        TryRewrite = Resolve;
        FixAll = new(TryRewrite);
    }

    /// <summary>Gets the applicability check, which builds no syntax.</summary>
    internal Func<SyntaxNode, Diagnostic, bool> CanRewrite { get; }

    /// <summary>Gets the edit derivation that resolves the reported node and builds its split form.</summary>
    internal Func<SyntaxNode, Diagnostic, NodeReplacement?> TryRewrite { get; }

    /// <summary>Gets the fix-all provider that batches the split across a document.</summary>
    internal BatchEditFixAllProvider FixAll { get; }

    /// <summary>Rewrites one node in a document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="node">The node to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Task<Document> FixAsync(Document document, SyntaxNode root, TNode node) =>
        Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, _rewrite(node))));

    /// <summary>Returns the node at the diagnostic's span when it has the reported type.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported node, or <see langword="null"/> when the node there has another type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TNode? FindReportedNode(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) as TNode;

    /// <summary>Resolves the reported node and builds its split form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private NodeReplacement? Resolve(SyntaxNode root, Diagnostic diagnostic) =>
        _resolve(root, diagnostic) is { } node
            ? new NodeReplacement(node, _rewrite(node), _rewriteCurrent)
            : null;
}
