// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Registers code fixes that edit the source text rather than the syntax tree.</summary>
internal static class TextChangeCodeFix
{
    /// <summary>The usual number of edits one diagnostic produces — a span and its counterpart.</summary>
    private const int InitialChangeCapacity = 2;

    /// <summary>Registers one text-editing code action per fixable diagnostic.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryAppendChanges">Appends a diagnostic's edits and returns whether the reported shape still matches.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SourceText, SyntaxNode, Diagnostic, List<TextChange>, bool> tryAppendChanges)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            // Probe against the document as it stands; the edits are derived again when invoked, because it may have moved on.
            var probe = new List<TextChange>(InitialChangeCapacity);
            if (!tryAppendChanges(text, root, diagnostic, probe))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => ApplyAsync(context.Document, diagnostic, tryAppendChanges, cancellationToken),
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// Registers one text-editing code action per diagnostic whose check words a title from the diagnostic alone, reading
    /// neither the syntax root nor the text until the action is applied.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="tryTitle">Words the code action for one diagnostic, or returns <see langword="null"/> when it cannot be fixed.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="appendChanges">Appends a diagnostic's edits, derived again when the action is applied.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static Task RegisterAsync(
        CodeFixContext context,
        Func<Diagnostic, string?> tryTitle,
        string equivalenceKey,
        Action<SourceText, SyntaxNode, Diagnostic, List<TextChange>> appendChanges)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (tryTitle(diagnostic) is not { } title)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => ApplyAsync(context.Document, diagnostic, appendChanges, cancellationToken),
                    equivalenceKey),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers one text-editing code action per diagnostic whose syntactic check words a title, reading the text only
    /// when the action is applied.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="tryTitle">Words the code action for one diagnostic, or returns <see langword="null"/> when its shape no longer matches.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="appendChanges">Appends a diagnostic's edits, derived again when the action is applied.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<SyntaxNode, Diagnostic, string?> tryTitle,
        string equivalenceKey,
        Action<SourceText, SyntaxNode, Diagnostic, List<TextChange>> appendChanges)
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
                    cancellationToken => ApplyAsync(context.Document, diagnostic, appendChanges, cancellationToken),
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// Registers one text-editing code action per diagnostic whose check against the text words a title, reading the syntax
    /// root only when the action is applied.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="tryTitle">Words the code action for one diagnostic, or returns <see langword="null"/> when its shape no longer matches.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="appendChanges">Appends a diagnostic's edits, derived again when the action is applied.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<SourceText, Diagnostic, string?> tryTitle,
        string equivalenceKey,
        Action<SourceText, SyntaxNode, Diagnostic, List<TextChange>> appendChanges)
    {
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (tryTitle(text, diagnostic) is not { } title)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => ApplyAsync(context.Document, diagnostic, appendChanges, cancellationToken),
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Registers one text-editing code action per diagnostic whose check against the text and root words a title.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="tryTitle">Words the code action for one diagnostic, or returns <see langword="null"/> when its shape no longer matches.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="appendChanges">Appends a diagnostic's edits, derived again when the action is applied.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<SourceText, SyntaxNode, Diagnostic, string?> tryTitle,
        string equivalenceKey,
        Action<SourceText, SyntaxNode, Diagnostic, List<TextChange>> appendChanges)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (tryTitle(text, root, diagnostic) is not { } title)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => ApplyAsync(context.Document, diagnostic, appendChanges, cancellationToken),
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Derives one diagnostic's edits against the current document and writes them.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="appendChanges">Appends the diagnostic's edits.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document, or the original when no edit applies.</returns>
    internal static async Task<Document> ApplyAsync(
        Document document,
        Diagnostic diagnostic,
        Action<SourceText, SyntaxNode, Diagnostic, List<TextChange>> appendChanges,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var changes = new List<TextChange>(InitialChangeCapacity);
        appendChanges(text, root, diagnostic, changes);
        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Re-derives one diagnostic's edits against the current document and writes them.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="tryAppendChanges">Appends a diagnostic's edits and returns whether the reported shape still matches.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document, or the original when the shape no longer matches.</returns>
    private static async Task<Document> ApplyAsync(
        Document document,
        Diagnostic diagnostic,
        Func<SourceText, SyntaxNode, Diagnostic, List<TextChange>, bool> tryAppendChanges,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var changes = new List<TextChange>(InitialChangeCapacity);
        return tryAppendChanges(text, root, diagnostic, changes)
            ? document.WithText(text.WithChanges(changes))
            : document;
    }
}
