// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Applies every diagnostic in a document through one <see cref="DocumentEditor"/>, for edits that need the semantic model or options.</summary>
internal sealed class AsyncBatchEditFixAllProvider : DocumentBasedFixAllProvider
{
    /// <summary>Registers one diagnostic's edits.</summary>
    private readonly Func<DocumentEditor, Diagnostic, CancellationToken, Task> _registerEditsAsync;

    /// <summary>Initializes a new instance of the <see cref="AsyncBatchEditFixAllProvider"/> class.</summary>
    /// <param name="registerEditsAsync">Registers one diagnostic's edits against the editor's original root.</param>
    internal AsyncBatchEditFixAllProvider(Func<DocumentEditor, Diagnostic, CancellationToken, Task> registerEditsAsync) =>
        _registerEditsAsync = registerEditsAsync;

    /// <inheritdoc/>
    protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty)
        {
            return document;
        }

        var editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in BatchEditFixAllProvider.UniqueDiagnostics(diagnostics))
        {
            await _registerEditsAsync(editor, diagnostic, fixAllContext.CancellationToken).ConfigureAwait(false);
        }

        return editor.GetChangedDocument();
    }
}
