// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// A shared <see cref="DocumentBasedFixAllProvider"/> for code fixes that change only the document
/// they are reported in. For each document it re-invokes the owning fix once per diagnostic against
/// the original text, collects every resulting text change, and applies the non-overlapping ones in a
/// single pass. Reusing the provider's own <see cref="CodeFixProvider.RegisterCodeFixesAsync"/> keeps
/// each rule's fix logic in one place while giving every fix an explicit, document-scoped Fix All.
/// </summary>
/// <remarks>
/// Only suitable for fixes whose change stays inside the reported document. Fixes that add, remove or
/// rename documents (or otherwise return a multi-document <see cref="Solution"/>) need a dedicated
/// solution-scoped provider instead.
/// </remarks>
internal sealed class DocumentTextFixAllProvider : DocumentBasedFixAllProvider
{
    /// <summary>The shared provider instance.</summary>
    public static readonly DocumentTextFixAllProvider Instance = new();

    /// <inheritdoc/>
    protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty)
        {
            return document;
        }

        var text = await document.GetTextAsync(fixAllContext.CancellationToken).ConfigureAwait(false);

        // Each fix is computed against the original document, so every diagnostic's span stays valid.
        var changes = new List<TextChange>();
        foreach (var diagnostic in diagnostics)
        {
            var changed = await ApplyOneAsync(fixAllContext, document, diagnostic).ConfigureAwait(false);
            if (changed is null)
            {
                continue;
            }

            foreach (var change in await changed.GetTextChangesAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false))
            {
                changes.Add(change);
            }
        }

        return changes.Count == 0 ? document : document.WithText(text.WithChanges(Merge(changes)));
    }

    /// <summary>Re-runs the owning fix for one diagnostic and returns the resulting document, or <see langword="null"/>.</summary>
    /// <param name="fixAllContext">The Fix All context (supplies the owning code-fix provider).</param>
    /// <param name="document">The original document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The changed document, or <see langword="null"/> when no matching fix was registered.</returns>
    private static async Task<Document?> ApplyOneAsync(FixAllContext fixAllContext, Document document, Diagnostic diagnostic)
    {
        CodeAction? selected = null;
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) =>
            {
                if (selected is null && (fixAllContext.CodeActionEquivalenceKey is null || fixAllContext.CodeActionEquivalenceKey == action.EquivalenceKey))
                {
                    selected = action;
                }
            },
            fixAllContext.CancellationToken);

        await fixAllContext.CodeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        if (selected is null)
        {
            return null;
        }

        var operations = await selected.GetOperationsAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
        for (var index = 0; index < operations.Length; index++)
        {
            if (operations[index] is ApplyChangesOperation apply)
            {
                return apply.ChangedSolution.GetDocument(document.Id);
            }
        }

        return null;
    }

    /// <summary>Sorts the text changes and drops any that overlap an already-kept change (first wins).</summary>
    /// <param name="changes">The collected text changes.</param>
    /// <returns>The non-overlapping changes in document order.</returns>
    private static List<TextChange> Merge(List<TextChange> changes)
    {
        changes.Sort(static (left, right) => left.Span.Start.CompareTo(right.Span.Start));

        var merged = new List<TextChange>(changes.Count);
        var lastEnd = -1;
        foreach (var change in changes)
        {
            if (change.Span.Start < lastEnd)
            {
                continue;
            }

            merged.Add(change);
            lastEnd = change.Span.End;
        }

        return merged;
    }
}
