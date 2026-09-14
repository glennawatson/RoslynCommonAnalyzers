// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>Registers and batch-applies code fixes whose edit replaces one node per diagnostic.</summary>
internal static class ReplaceNodeCodeFix
{
    /// <summary>Registers applicable fixes and defers replacement construction until invocation.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="canRewrite">Checks the rewriter's applicability without building syntax.</param>
    /// <param name="tryRewrite">The provider's edit derivation, invoked only when applying.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>Loops itself rather than forwarding to the diagnostic-titled overload, which would wrap the title and key in delegates on every call.</remarks>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, bool> canRewrite,
        Func<SyntaxNode, Diagnostic, NodeReplacement?> tryRewrite)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!canRewrite(root, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(Apply(context.Document, root, diagnostic, tryRewrite));
                    },
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers deferred fixes with diagnostic-specific titles and equivalence keys.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title for one diagnostic.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="canRewrite">Checks the rewriter's applicability without building syntax.</param>
    /// <param name="tryRewrite">The provider's edit derivation, invoked only when applying.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<Diagnostic, string> title,
        Func<Diagnostic, string> equivalenceKey,
        Func<SyntaxNode, Diagnostic, bool> canRewrite,
        Func<SyntaxNode, Diagnostic, NodeReplacement?> tryRewrite)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!canRewrite(root, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title(diagnostic),
                    cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(Apply(context.Document, root, diagnostic, tryRewrite));
                    },
                    equivalenceKey(diagnostic)),
                diagnostic);
        }
    }

    /// <summary>Registers deferred fixes whose applicability and rewrite need semantic information.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="canRewrite">Checks the rewriter's applicability without building syntax.</param>
    /// <param name="tryRewrite">The provider's edit derivation, invoked only when applying.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, SemanticModel, Diagnostic, bool> canRewrite,
        Func<SyntaxNode, SemanticModel, Diagnostic, NodeReplacement?> tryRewrite)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!canRewrite(root, model, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(Apply(context.Document, root, model, diagnostic, tryRewrite));
                    },
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// Registers deferred fixes whose title is worded by the applicability check, so each diagnostic is resolved once
    /// when registering and the replacement is built only when the fix is applied.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="tryTitle">Words the code action for one diagnostic without building syntax, or returns <see langword="null"/> when its shape no longer matches.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="tryRewrite">The provider's edit derivation, invoked only when applying.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<SyntaxNode, Diagnostic, string?> tryTitle,
        Func<Diagnostic, string> equivalenceKey,
        Func<SyntaxNode, Diagnostic, NodeReplacement?> tryRewrite)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (tryTitle(root, diagnostic) is not { } title)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(Apply(context.Document, root, diagnostic, tryRewrite));
                    },
                    equivalenceKey(diagnostic)),
                diagnostic);
        }
    }

    /// <summary>
    /// Registers deferred fixes that need semantic information and whose title is worded by the applicability check,
    /// so each diagnostic is resolved once when registering and the replacement is built only when the fix is applied.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="tryTitle">Words the code action for one diagnostic, or returns <see langword="null"/> when its shape no longer matches.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="tryRewrite">The provider's edit derivation, invoked only when applying.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<SyntaxNode, SemanticModel, Diagnostic, string?> tryTitle,
        Func<Diagnostic, string> equivalenceKey,
        Func<SyntaxNode, SemanticModel, Diagnostic, NodeReplacement?> tryRewrite)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (tryTitle(root, model, diagnostic) is not { } title)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(Apply(context.Document, root, model, diagnostic, tryRewrite));
                    },
                    equivalenceKey(diagnostic)),
                diagnostic);
        }
    }

    /// <summary>Registers one replace-node code action per fixable diagnostic.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Use only when applicability requires constructing and validating the replacement. Loops itself rather than forwarding
    /// to the diagnostic-titled overload, which would wrap the title and key in delegates on every call.
    /// </remarks>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, NodeReplacement?> tryRewrite)
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
                    title,
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))),
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// Registers one replace-node code action per fixable diagnostic, wording the action and grouping it
    /// from the diagnostic. A provider that spans ids naming different repairs needs both to vary.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title for one diagnostic.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>Use only when applicability requires constructing and validating the replacement.</remarks>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<Diagnostic, string> title,
        Func<Diagnostic, string> equivalenceKey,
        Func<SyntaxNode, Diagnostic, NodeReplacement?> tryRewrite)
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
    /// <remarks>Use only when applicability requires constructing and validating the replacement.</remarks>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, SemanticModel, Diagnostic, NodeReplacement?> tryRewrite)
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
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic, Func<SyntaxNode, Diagnostic, NodeReplacement?> tryRewrite) =>
        tryRewrite(root, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Applies one diagnostic's semantic replacement to a document outside the registration path.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    /// <returns>The updated document, or the original when the reported shape no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, Diagnostic diagnostic, Func<SyntaxNode, SemanticModel, Diagnostic, NodeReplacement?> tryRewrite) =>
        tryRewrite(root, model, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Applies one diagnostic's replacement inside a batch fix-all edit.</summary>
    /// <param name="editor">The document editor.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    internal static void ApplyBatchEdit(DocumentEditor editor, Diagnostic diagnostic, Func<SyntaxNode, Diagnostic, NodeReplacement?> tryRewrite)
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
    internal static void ApplyBatchEdit(DocumentEditor editor, Diagnostic diagnostic, Func<SyntaxNode, SemanticModel, Diagnostic, NodeReplacement?> tryRewrite)
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
