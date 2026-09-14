// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Applies every diagnostic's text changes to a document in one pass, dropping changes that overlap one already kept.</summary>
internal sealed class TextChangeBatchFixAllProvider : DocumentBasedFixAllProvider
{
    /// <summary>Adds one diagnostic's text changes.</summary>
    private readonly Action<SourceText, SyntaxNode, Diagnostic, List<TextChange>> _registerTextChanges;

    /// <summary>Initializes a new instance of the <see cref="TextChangeBatchFixAllProvider"/> class.</summary>
    /// <param name="registerTextChanges">Adds the text changes that fix one diagnostic, computed against the original text and root.</param>
    internal TextChangeBatchFixAllProvider(Action<SourceText, SyntaxNode, Diagnostic, List<TextChange>> registerTextChanges) =>
        _registerTextChanges = registerTextChanges;

    /// <summary>Initializes a new instance of the <see cref="TextChangeBatchFixAllProvider"/> class from a provider's change appender.</summary>
    /// <param name="tryAppendChanges">Adds the text changes that fix one diagnostic and reports whether the reported shape still matched.</param>
    internal TextChangeBatchFixAllProvider(Func<SourceText, SyntaxNode, Diagnostic, List<TextChange>, bool> tryAppendChanges)
        : this((text, root, diagnostic, changes) => { _ = tryAppendChanges(text, root, diagnostic, changes); })
    {
    }

    /// <inheritdoc/>
    protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty)
        {
            return document;
        }

        var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var changes = new List<TextChange>(diagnostics.Length);
        foreach (var diagnostic in BatchEditFixAllProvider.UniqueDiagnostics(diagnostics))
        {
            _registerTextChanges(text, root, diagnostic, changes);
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
