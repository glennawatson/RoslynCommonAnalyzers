// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>
/// A code fix that can register all of one document's edits onto a shared <see cref="DocumentEditor"/>.
/// Implemented alongside <see cref="CodeFixProvider"/> so the same per-diagnostic transform drives both
/// the single-diagnostic fix and the batched <see cref="BatchEditFixAllProvider"/> Fix All.
/// </summary>
internal interface IBatchFixableCodeFix
{
    /// <summary>Registers the edits for one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor accumulating every edit for the document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic);
}
