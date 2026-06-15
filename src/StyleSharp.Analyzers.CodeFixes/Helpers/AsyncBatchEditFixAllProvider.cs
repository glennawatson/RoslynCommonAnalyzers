// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>
/// A <see cref="DocumentBasedFixAllProvider"/> for node-edit fixes that need a semantic model or
/// analyzer options. It creates one <see cref="DocumentEditor"/> and lets the owning fix register each
/// diagnostic's edits (awaiting <see cref="DocumentEditor.OriginalDocument"/> for the model/options as
/// needed), then materialises the changed document once — instead of
/// <see cref="WellKnownFixAllProviders.BatchFixer"/> cloning and re-parsing the document per diagnostic.
/// </summary>
internal sealed class AsyncBatchEditFixAllProvider : DocumentBasedFixAllProvider
{
    /// <summary>The shared provider instance.</summary>
    public static readonly AsyncBatchEditFixAllProvider Instance = new();

    /// <inheritdoc/>
    protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty || fixAllContext.CodeFixProvider is not IAsyncBatchableCodeFix fix)
        {
            return document;
        }

        var editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in diagnostics)
        {
            await fix.RegisterEditsAsync(editor, diagnostic, fixAllContext.CancellationToken).ConfigureAwait(false);
        }

        return editor.GetChangedDocument();
    }
}
