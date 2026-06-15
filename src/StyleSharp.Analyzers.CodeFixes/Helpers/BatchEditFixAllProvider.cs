// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>
/// A <see cref="DocumentBasedFixAllProvider"/> that applies every diagnostic in a document in a single
/// pass: it creates one <see cref="DocumentEditor"/> and lets the owning fix register each diagnostic's
/// edits against the original root, then materialises the changed document once. This avoids
/// <see cref="WellKnownFixAllProviders.BatchFixer"/>'s clone-and-reparse of the document per diagnostic.
/// </summary>
internal sealed class BatchEditFixAllProvider : DocumentBasedFixAllProvider
{
    /// <summary>The shared provider instance.</summary>
    public static readonly BatchEditFixAllProvider Instance = new();

    /// <inheritdoc/>
    protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty || fixAllContext.CodeFixProvider is not IBatchFixableCodeFix fix)
        {
            return document;
        }

        var editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in diagnostics)
        {
            fix.RegisterBatchEdits(editor, diagnostic);
        }

        return editor.GetChangedDocument();
    }
}
