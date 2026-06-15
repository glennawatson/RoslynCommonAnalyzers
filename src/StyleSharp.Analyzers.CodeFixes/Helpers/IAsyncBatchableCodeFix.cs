// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix whose per-diagnostic edit needs asynchronous context — a semantic model or analyzer
/// options — reached through the editor's <see cref="DocumentEditor.OriginalDocument"/>. Implemented
/// alongside <see cref="CodeFixProvider"/> so the same edit drives both the single-diagnostic fix and
/// the batched <see cref="AsyncBatchEditFixAllProvider"/> Fix All.
/// </summary>
internal interface IAsyncBatchableCodeFix
{
    /// <summary>Registers the edits for one diagnostic against the shared editor.</summary>
    /// <param name="editor">The shared document editor accumulating every edit for the document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RegisterEditsAsync(DocumentEditor editor, Diagnostic diagnostic, CancellationToken cancellationToken);
}
