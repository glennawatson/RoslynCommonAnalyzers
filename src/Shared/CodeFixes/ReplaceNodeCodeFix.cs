// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>
/// Runs the registration skeleton shared by every single-node-replacement code fix: resolve
/// the syntax root (and semantic model when the rewriter needs one), re-derive the edit from
/// each diagnostic, and register one code action that swaps the node — with a matching batch
/// entry point for <see cref="BatchEditFixAllProvider"/>. Providers keep only their shape
/// re-validation and replacement construction.
/// </summary>
internal static class ReplaceNodeCodeFix
{
    /// <summary>Computes a node replacement from the root and one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal delegate NodeReplacement? SyntaxRewriter(SyntaxNode root, Diagnostic diagnostic);

    /// <summary>Computes a node replacement that needs the semantic model.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal delegate NodeReplacement? SemanticRewriter(SyntaxNode root, SemanticModel model, Diagnostic diagnostic);

    /// <summary>Builds the code action title for one diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <returns>The code action title.</returns>
    internal delegate string TitleFactory(Diagnostic diagnostic);

    /// <summary>Registers one replace-node code action per fixable diagnostic.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Task RegisterAsync(in CodeFixContext context, string title, string equivalenceKey, SyntaxRewriter tryRewrite) =>
        RegisterAsync(context, _ => title, _ => equivalenceKey, tryRewrite);

    /// <summary>
    /// Registers one replace-node code action per fixable diagnostic, wording the action and grouping it
    /// from the diagnostic. A provider that spans ids naming different repairs needs both to vary.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title for one diagnostic.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(CodeFixContext context, TitleFactory title, TitleFactory equivalenceKey, SyntaxRewriter tryRewrite)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (tryRewrite(root, diagnostic) is not { } edit)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title(diagnostic),
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))),
                    equivalenceKey(diagnostic)),
                diagnostic);
        }
    }

    /// <summary>Registers one replace-node code action per fixable diagnostic, with semantic model access.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(CodeFixContext context, string title, string equivalenceKey, SemanticRewriter tryRewrite)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (tryRewrite(root, model, diagnostic) is not { } edit)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))),
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Applies one diagnostic's replacement to a document outside the registration path.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    /// <returns>The updated document, or the original when no edit applies.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic, SyntaxRewriter tryRewrite) =>
        tryRewrite(root, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Applies one diagnostic's replacement inside a batch fix-all edit.</summary>
    /// <param name="editor">The document editor.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    internal static void ApplyBatchEdit(DocumentEditor editor, Diagnostic diagnostic, SyntaxRewriter tryRewrite)
    {
        if (tryRewrite(editor.OriginalRoot, diagnostic) is not { } edit)
        {
            return;
        }

        if (edit.RewriteCurrent is { } rewriteCurrent)
        {
            editor.ReplaceNode(edit.Original, (current, _) => rewriteCurrent(current));
            return;
        }

        editor.ReplaceNode(edit.Original, edit.Replacement);
    }

    /// <summary>Applies one diagnostic's replacement inside a batch fix-all edit, with semantic model access.</summary>
    /// <param name="editor">The document editor.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    internal static void ApplyBatchEdit(DocumentEditor editor, Diagnostic diagnostic, SemanticRewriter tryRewrite)
    {
        if (tryRewrite(editor.OriginalRoot, editor.SemanticModel, diagnostic) is not { } edit)
        {
            return;
        }

        if (edit.RewriteCurrent is { } rewriteCurrent)
        {
            editor.ReplaceNode(edit.Original, (current, _) => rewriteCurrent(current));
            return;
        }

        editor.ReplaceNode(edit.Original, edit.Replacement);
    }
}
