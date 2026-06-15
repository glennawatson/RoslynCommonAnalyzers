// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// A <see cref="DocumentBasedFixAllProvider"/> for fixes that edit the document's text. It computes
/// every diagnostic's <see cref="TextChange"/>s against the original text, drops any that overlap an
/// already-kept change, and applies the rest in a single <see cref="SourceText.WithChanges(IEnumerable{TextChange})"/>
/// call — instead of <see cref="WellKnownFixAllProviders.BatchFixer"/> cloning and re-parsing the
/// document once per diagnostic. This is where the highest-frequency layout and spacing rules live.
/// </summary>
internal sealed class TextChangeBatchFixAllProvider : DocumentBasedFixAllProvider
{
    /// <summary>The shared provider instance.</summary>
    public static readonly TextChangeBatchFixAllProvider Instance = new();

    /// <inheritdoc/>
    protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty || fixAllContext.CodeFixProvider is not ITextChangeBatchableCodeFix fix)
        {
            return document;
        }

        var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var changes = new List<TextChange>();
        foreach (var diagnostic in diagnostics)
        {
            fix.RegisterTextChanges(text, root, diagnostic, changes);
        }

        return changes.Count == 0 ? document : document.WithText(text.WithChanges(Merge(changes)));
    }

    /// <summary>Sorts the changes and drops any that overlap an already-kept change (first wins).</summary>
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
