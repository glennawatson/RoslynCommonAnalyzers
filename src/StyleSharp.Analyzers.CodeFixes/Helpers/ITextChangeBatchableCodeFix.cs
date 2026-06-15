// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix whose change is a set of <see cref="TextChange"/>s rather than a syntax-node edit.
/// Implemented alongside <see cref="CodeFixProvider"/> so the same per-diagnostic computation drives
/// both the single-diagnostic fix and the batched <see cref="TextChangeBatchFixAllProvider"/> Fix All.
/// </summary>
internal interface ITextChangeBatchableCodeFix
{
    /// <summary>Adds the text changes that fix one diagnostic to the shared list.</summary>
    /// <param name="text">The document's original source text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The accumulating list of text changes for the whole document.</param>
    void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes);
}
