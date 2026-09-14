// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>
/// Runs the registration skeleton shared by code fixes that act on one resolved target per diagnostic:
/// resolve the syntax root (and semantic model when the resolver needs one), resolve each diagnostic's
/// target, and register one code action that applies the provider's edit to it when invoked.
/// </summary>
/// <remarks>
/// The target is resolved once, when the lightbulb is populated, and the edit is built only when the action
/// runs. A diagnostic whose target no longer resolves registers nothing. Providers whose edit is a single node
/// swap with a batch counterpart use <see cref="ReplaceNodeCodeFix"/> instead.
/// </remarks>
internal static class TargetCodeFix
{
    /// <summary>Resolves the target a diagnostic asks to fix.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The target, or <see langword="null"/> when the reported shape no longer matches.</returns>
    internal delegate TTarget? Resolver<TTarget>(SyntaxNode root, Diagnostic diagnostic)
        where TTarget : class;

    /// <summary>Resolves the target a diagnostic asks to fix.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="target">The resolved target.</param>
    /// <returns><see langword="true"/> when the reported shape still matches.</returns>
    internal delegate bool TryResolver<TTarget>(SyntaxNode root, Diagnostic diagnostic, [NotNullWhen(true)] out TTarget? target);

    /// <summary>Resolves the target a diagnostic asks to fix, with semantic model access.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">A token that cancels binding.</param>
    /// <param name="target">The resolved target.</param>
    /// <returns><see langword="true"/> when the reported shape still matches.</returns>
    internal delegate bool SemanticTryResolver<TTarget>(SyntaxNode root, SemanticModel model, Diagnostic diagnostic, CancellationToken cancellationToken, [NotNullWhen(true)] out TTarget? target);

    /// <summary>Applies the provider's edit to a resolved target.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root the target was resolved against.</param>
    /// <param name="target">The resolved target.</param>
    /// <returns>The updated document.</returns>
    internal delegate Document Applier<TTarget>(Document document, SyntaxNode root, TTarget target);

    /// <summary>Applies the provider's edit to a resolved target asynchronously.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="document">The document being fixed.</param>
    /// <param name="target">The resolved target.</param>
    /// <param name="cancellationToken">A token that cancels the edit.</param>
    /// <returns>The updated document.</returns>
    internal delegate Task<Document> AsyncApplier<TTarget>(Document document, TTarget target, CancellationToken cancellationToken);

    /// <summary>Builds a code action title from a resolved target.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="target">The resolved target.</param>
    /// <returns>The code action title.</returns>
    internal delegate string TargetTitle<TTarget>(TTarget target);

    /// <summary>Builds a code action title or equivalence key from the diagnostic being fixed.</summary>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <returns>The text, or <see langword="null"/> when the diagnostic has no fix to offer.</returns>
    internal delegate string? DiagnosticText(Diagnostic diagnostic);

    /// <summary>Registers one code action per diagnostic whose target resolves.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves each diagnostic's target.</param>
    /// <param name="apply">Builds the edit for a resolved target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(CodeFixContext context, string title, string equivalenceKey, Resolver<TTarget> resolve, Applier<TTarget> apply)
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

    /// <summary>Registers one code action per diagnostic whose target resolves, applying the edit asynchronously.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves each diagnostic's target.</param>
    /// <param name="apply">Builds the edit for a resolved target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(CodeFixContext context, string title, string equivalenceKey, Resolver<TTarget> resolve, AsyncApplier<TTarget> apply)
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
    /// <param name="tryResolve">Resolves each diagnostic's target.</param>
    /// <param name="apply">Builds the edit for a resolved target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(CodeFixContext context, string title, string equivalenceKey, TryResolver<TTarget> tryResolve, Applier<TTarget> apply)
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

    /// <summary>Registers one code action per diagnostic whose target resolves, applying the edit asynchronously.</summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryResolve">Resolves each diagnostic's target.</param>
    /// <param name="apply">Builds the edit for a resolved target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(CodeFixContext context, string title, string equivalenceKey, TryResolver<TTarget> tryResolve, AsyncApplier<TTarget> apply)
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
    /// <param name="title">Builds the code action title from the resolved target.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves each diagnostic's target.</param>
    /// <param name="apply">Builds the edit for a resolved target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(CodeFixContext context, TargetTitle<TTarget> title, string equivalenceKey, Resolver<TTarget> resolve, Applier<TTarget> apply)
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

    /// <summary>
    /// Registers one code action per diagnostic whose target resolves, titled and grouped from the diagnostic. A
    /// provider that spans ids naming different repairs needs both to vary.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title; a diagnostic with no title registers nothing.</param>
    /// <param name="equivalenceKey">Builds the equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves each diagnostic's target.</param>
    /// <param name="apply">Builds the edit for a resolved target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(CodeFixContext context, DiagnosticText title, DiagnosticText equivalenceKey, Resolver<TTarget> resolve, Applier<TTarget> apply)
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
    /// <param name="tryResolve">Resolves each diagnostic's target.</param>
    /// <param name="apply">Builds the edit for a resolved target.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync<TTarget>(CodeFixContext context, string title, string equivalenceKey, SemanticTryResolver<TTarget> tryResolve, Applier<TTarget> apply)
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
}
