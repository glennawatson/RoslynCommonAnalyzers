// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>Registers code fixes that resolve one target per diagnostic and build the edit only when the action runs.</summary>
internal static class TargetCodeFix
{
    /// <summary>Replaces a resolved node with its rewrite in the document.</summary>
    /// <typeparam name="TTarget">The target node type.</typeparam>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root the target belongs to.</param>
    /// <param name="target">The node to replace.</param>
    /// <param name="rewrite">Builds the node that replaces the target.</param>
    /// <returns>The updated document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Document Apply<TTarget>(Document document, SyntaxNode root, TTarget target, Func<TTarget, SyntaxNode> rewrite)
        where TTarget : SyntaxNode =>
        document.WithSyntaxRoot(root.ReplaceNode(target, rewrite(target)));

    /// <summary>Registers one code action per diagnostic whose target resolves, replacing the target with its rewrite when the action runs.</summary>
    /// <typeparam name="TTarget">The target node type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves a diagnostic's target, or <see langword="null"/> when the shape no longer matches.</param>
    /// <param name="rewrite">Builds the node that replaces the target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, TTarget?> resolve,
        Func<TTarget, SyntaxNode> rewrite)
        where TTarget : SyntaxNode
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (resolve(root, diagnostic) is not { } target)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, _ => Task.FromResult(Apply(context.Document, root, target, rewrite)), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// Registers one code action per diagnostic whose target resolves against the semantic model, replacing the target with its
    /// rewrite when the action runs.
    /// </summary>
    /// <typeparam name="TTarget">The target node type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryResolve">Resolves a diagnostic's target and returns whether the shape still matches.</param>
    /// <param name="rewrite">Builds the node that replaces the target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        TryFunc<SyntaxNode, SemanticModel, Diagnostic, CancellationToken, TTarget> tryResolve,
        Func<TTarget, SyntaxNode> rewrite)
        where TTarget : SyntaxNode
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!tryResolve(root, model, diagnostic, context.CancellationToken, out var target))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, _ => Task.FromResult(Apply(context.Document, root, target, rewrite)), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic whose target resolves.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves a diagnostic's target, or <see langword="null"/> when the shape no longer matches.</param>
    /// <param name="apply">Builds the edited document for a target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, TTarget?> resolve,
        Func<Document, SyntaxNode, TTarget, Document> apply)
        where TTarget : class
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (resolve(root, diagnostic) is not { } target)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, _ => Task.FromResult(apply(context.Document, root, target)), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic whose target resolves, building the edit asynchronously.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves a diagnostic's target, or <see langword="null"/> when the shape no longer matches.</param>
    /// <param name="apply">Builds the edited document for a target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, TTarget?> resolve,
        Func<Document, TTarget, CancellationToken, Task<Document>> apply)
        where TTarget : class
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (resolve(root, diagnostic) is not { } target)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, cancellationToken => apply(context.Document, target, cancellationToken), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic whose target resolves.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryResolve">Resolves a diagnostic's target and returns whether the shape still matches.</param>
    /// <param name="apply">Builds the edited document for a target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        TryFunc<SyntaxNode, Diagnostic, TTarget> tryResolve,
        Func<Document, SyntaxNode, TTarget, Document> apply)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!tryResolve(root, diagnostic, out var target))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, _ => Task.FromResult(apply(context.Document, root, target)), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic whose target resolves, building the edit asynchronously.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryResolve">Resolves a diagnostic's target and returns whether the shape still matches.</param>
    /// <param name="apply">Builds the edited document for a target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        TryFunc<SyntaxNode, Diagnostic, TTarget> tryResolve,
        Func<Document, TTarget, CancellationToken, Task<Document>> apply)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!tryResolve(root, diagnostic, out var target))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, cancellationToken => apply(context.Document, target, cancellationToken), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic whose target resolves, titled from the target.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title from a target.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves a diagnostic's target, or <see langword="null"/> when the shape no longer matches.</param>
    /// <param name="apply">Builds the edited document for a target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        Func<TTarget, string> title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, TTarget?> resolve,
        Func<Document, SyntaxNode, TTarget, Document> apply)
        where TTarget : class
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (resolve(root, diagnostic) is not { } target)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title(target), _ => Task.FromResult(apply(context.Document, root, target)), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic whose target resolves, titled and grouped from the diagnostic.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title; a diagnostic without one registers nothing.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves a diagnostic's target, or <see langword="null"/> when the shape no longer matches.</param>
    /// <param name="apply">Builds the edited document for a target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        Func<Diagnostic, string?> title,
        Func<Diagnostic, string?> equivalenceKey,
        Func<SyntaxNode, Diagnostic, TTarget?> resolve,
        Func<Document, SyntaxNode, TTarget, Document> apply)
        where TTarget : class
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (title(diagnostic) is not { } text || resolve(root, diagnostic) is not { } target)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(text, _ => Task.FromResult(apply(context.Document, root, target)), equivalenceKey(diagnostic)),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic whose target resolves against the semantic model.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryResolve">Resolves a diagnostic's target and returns whether the shape still matches.</param>
    /// <param name="apply">Builds the edited document for a target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        TryFunc<SyntaxNode, SemanticModel, Diagnostic, CancellationToken, TTarget> tryResolve,
        Func<Document, SyntaxNode, TTarget, Document> apply)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!tryResolve(root, model, diagnostic, context.CancellationToken, out var target))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, _ => Task.FromResult(apply(context.Document, root, target)), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic that still matches, re-resolving the diagnostic when the action runs.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="canFix">Returns whether a diagnostic's shape still matches, without building the edit.</param>
    /// <param name="apply">Builds the edited document for a diagnostic.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, bool> canFix,
        Func<Document, SyntaxNode, Diagnostic, Document> apply)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!canFix(root, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, _ => Task.FromResult(apply(context.Document, root, diagnostic)), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic that still matches, re-resolving the diagnostic asynchronously when the action runs.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="canFix">Returns whether a diagnostic's shape still matches, without building the edit.</param>
    /// <param name="apply">Builds the edited document for a diagnostic.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, bool> canFix,
        Func<Document, Diagnostic, CancellationToken, Task<Document>> apply)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!canFix(root, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(title, cancellationToken => apply(context.Document, diagnostic, cancellationToken), equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic that still matches, titled and grouped from the diagnostic.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title; a diagnostic without one registers nothing.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="canFix">Returns whether a diagnostic's shape still matches, without building the edit.</param>
    /// <param name="apply">Builds the edited document for a diagnostic.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<Diagnostic, string?> title,
        Func<Diagnostic, string?> equivalenceKey,
        Func<SyntaxNode, Diagnostic, bool> canFix,
        Func<Document, SyntaxNode, Diagnostic, Document> apply)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (title(diagnostic) is not { } text || !canFix(root, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(text, _ => Task.FromResult(apply(context.Document, root, diagnostic)), equivalenceKey(diagnostic)),
                diagnostic);
        }
    }

    /// <summary>Registers one code action per diagnostic that still matches, titled and grouped from the diagnostic and building the edit asynchronously.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title; a diagnostic without one registers nothing.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="canFix">Returns whether a diagnostic's shape still matches, without building the edit.</param>
    /// <param name="apply">Builds the edited document for a diagnostic.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<Diagnostic, string?> title,
        Func<Diagnostic, string?> equivalenceKey,
        Func<SyntaxNode, Diagnostic, bool> canFix,
        Func<Document, SyntaxNode, Diagnostic, CancellationToken, Task<Document>> apply)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (title(diagnostic) is not { } text || !canFix(root, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(text, cancellationToken => apply(context.Document, root, diagnostic, cancellationToken), equivalenceKey(diagnostic)),
                diagnostic);
        }
    }

    /// <summary>Registers one code action for every diagnostic, building the edit from the diagnostic when the action runs.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="apply">Builds the edited document for a diagnostic.</param>
    /// <returns>A completed task.</returns>
    internal static Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<Document, Diagnostic, CancellationToken, Task<Document>> apply)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(title, cancellationToken => apply(context.Document, diagnostic, cancellationToken), equivalenceKey),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>Registers one code action for every diagnostic, titled from the diagnostic and building the edit from it when the action runs.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title from a diagnostic.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="apply">Builds the edited document for a diagnostic.</param>
    /// <returns>A completed task.</returns>
    internal static Task RegisterAsync(
        CodeFixContext context,
        Func<Diagnostic, string> title,
        string equivalenceKey,
        Func<Document, Diagnostic, CancellationToken, Task<Document>> apply)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(title(diagnostic), cancellationToken => apply(context.Document, diagnostic, cancellationToken), equivalenceKey),
                diagnostic);
        }

        return Task.CompletedTask;
    }
}
