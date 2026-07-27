// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Runs the registration skeleton shared by every code fix that edits the source text rather than the
/// syntax tree: resolve the root and text, probe the diagnostic to see whether the edit still applies,
/// and register one code action that re-derives the edits and writes them — with the same derivation
/// feeding <see cref="TextChangeBatchFixAllProvider"/>. Providers keep only their change building.
/// </summary>
/// <remarks>
/// The edits are derived twice on purpose: once against the document as it stands, to decide whether to
/// offer the fix at all, and again when the user invokes it, because the document may have moved on.
/// </remarks>
internal static class TextChangeCodeFix
{
    /// <summary>The usual number of edits one diagnostic produces — a span and its counterpart.</summary>
    private const int InitialChangeCapacity = 2;

    /// <summary>Appends the edits one diagnostic asks for.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="changes">The list the edits are appended to.</param>
    /// <returns><see langword="true"/> when the reported shape still matches and edits were appended.</returns>
    public delegate bool ChangeBuilder(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes);

    /// <summary>Registers one text-editing code action per fixable diagnostic.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="tryAppendChanges">The provider's edit derivation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task RegisterAsync(CodeFixContext context, string title, string equivalenceKey, ChangeBuilder tryAppendChanges)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
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

    /// <summary>Re-derives one diagnostic's edits against the current document and writes them.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="tryAppendChanges">The provider's edit derivation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document, or the original when the shape no longer matches.</returns>
    private static async Task<Document> ApplyAsync(
        Document document,
        Diagnostic diagnostic,
        ChangeBuilder tryAppendChanges,
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
