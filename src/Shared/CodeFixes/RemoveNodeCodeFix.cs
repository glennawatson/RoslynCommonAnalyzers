// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>
/// Runs the registration skeleton shared by every code fix whose edit is "delete this node": resolve
/// the syntax root (and semantic model when the selector needs one), re-derive the node from each
/// diagnostic, and register one code action that drops it — with a matching batch entry point for
/// <see cref="BatchEditFixAllProvider"/>. Providers keep only their shape re-validation.
/// </summary>
/// <remarks>
/// The replacement sibling is <see cref="ReplaceNodeCodeFix"/>; a deletion cannot go through it because
/// there is no replacement node to swap in and the removal options travel with the edit.
/// </remarks>
internal static class RemoveNodeCodeFix
{
    /// <summary>Resolves the node a diagnostic asks to delete.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to remove, or <see langword="null"/> when the shape no longer matches.</returns>
    public delegate NodeRemoval? NodeSelector(SyntaxNode root, Diagnostic diagnostic);

    /// <summary>Resolves the node a diagnostic asks to delete, with semantic model access.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to remove, or <see langword="null"/> when the shape no longer matches.</returns>
    public delegate NodeRemoval? SemanticNodeSelector(SyntaxNode root, SemanticModel model, Diagnostic diagnostic);

    /// <summary>Selects the reported node itself when it is of the expected kind.</summary>
    /// <typeparam name="T">The node type the diagnostic reports.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to remove, or <see langword="null"/> when the shape no longer matches.</returns>
    /// <remarks>Pass this as the selector when the diagnostic is reported on the node being deleted.</remarks>
    public static NodeRemoval? Node<T>(SyntaxNode root, Diagnostic diagnostic)
        where T : SyntaxNode
        => root.FindNode(diagnostic.Location.SourceSpan) is T node ? new NodeRemoval(node) : null;

    /// <summary>Selects the nearest enclosing node of the expected kind, starting at the reported node.</summary>
    /// <typeparam name="T">The declaration or statement type being deleted.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to remove, or <see langword="null"/> when the shape no longer matches.</returns>
    /// <remarks>
    /// Pass this as the selector when the diagnostic lands on a name or modifier inside the declaration
    /// that is actually being deleted.
    /// </remarks>
    public static NodeRemoval? Ancestor<T>(SyntaxNode root, Diagnostic diagnostic)
        where T : SyntaxNode
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<T>() is { } node
            ? new NodeRemoval(node)
            : null;

    /// <summary>Registers one remove-node code action per fixable diagnostic.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="trySelect">The provider's node resolution.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task RegisterAsync(CodeFixContext context, string title, string equivalenceKey, NodeSelector trySelect)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (trySelect(root, diagnostic) is not { } removal)
            {
                continue;
            }

            RegisterRemoval(context, root, removal, title, equivalenceKey, diagnostic);
        }
    }

    /// <summary>Registers one remove-node code action per fixable diagnostic, with semantic model access.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="trySelect">The provider's node resolution.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task RegisterAsync(CodeFixContext context, string title, string equivalenceKey, SemanticNodeSelector trySelect)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (trySelect(root, model, diagnostic) is not { } removal)
            {
                continue;
            }

            RegisterRemoval(context, root, removal, title, equivalenceKey, diagnostic);
        }
    }

    /// <summary>Applies one diagnostic's removal inside a batch fix-all edit.</summary>
    /// <param name="editor">The document editor.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="trySelect">The provider's node resolution.</param>
    public static void ApplyBatchEdit(DocumentEditor editor, Diagnostic diagnostic, NodeSelector trySelect)
    {
        if (trySelect(editor.OriginalRoot, diagnostic) is not { } removal)
        {
            return;
        }

        editor.RemoveNode(removal.Node, removal.Options);
    }

    /// <summary>Applies one diagnostic's removal inside a batch fix-all edit, with semantic model access.</summary>
    /// <param name="editor">The document editor.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="trySelect">The provider's node resolution.</param>
    public static void ApplyBatchEdit(DocumentEditor editor, Diagnostic diagnostic, SemanticNodeSelector trySelect)
    {
        if (trySelect(editor.OriginalRoot, editor.SemanticModel, diagnostic) is not { } removal)
        {
            return;
        }

        editor.RemoveNode(removal.Node, removal.Options);
    }

    /// <summary>Registers the code action that drops one resolved node.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="root">The syntax root the node came from.</param>
    /// <param name="removal">The node to drop.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <remarks>
    /// Removing every node from a root is not something a fix does, but the API allows it, so an empty
    /// result leaves the document untouched rather than throwing at the user.
    /// </remarks>
    private static void RegisterRemoval(
        CodeFixContext context,
        SyntaxNode root,
        NodeRemoval removal,
        string title,
        string equivalenceKey,
        Diagnostic diagnostic)
        => context.RegisterCodeFix(
            CodeAction.Create(
                title,
                cancellationToken => Task.FromResult(
                    root.RemoveNode(removal.Node, removal.Options) is { } updated
                        ? context.Document.WithSyntaxRoot(updated)
                        : context.Document),
                equivalenceKey),
            diagnostic);
}
