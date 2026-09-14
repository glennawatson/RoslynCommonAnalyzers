// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Removes the blank line before a chained <c>else</c>/<c>catch</c>/<c>finally</c> (SST1510) or the <c>while</c> footer of a do/while loop (SST1511).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChainedBlockSpacingCodeFixProvider))]
[Shared]
public sealed class ChainedBlockSpacingCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.ChainedBlockNotPrecededByBlankLine.Id,
        LayoutRules.WhileFooterNotPrecededByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            static _ => "Remove blank line before keyword",
            nameof(ChainedBlockSpacingCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryBuildChange(text, diagnostic.Location.SourceSpan, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Computes the change that removes the run of blank lines directly above the reported keyword.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="keywordSpan">The span of the reported keyword.</param>
    /// <param name="change">The computed text change when one applies.</param>
    /// <returns><see langword="true"/> when blank lines were found to remove.</returns>
    private static bool TryBuildChange(SourceText text, TextSpan keywordSpan, out TextChange change)
    {
        var keywordLine = text.Lines.GetLineFromPosition(keywordSpan.Start).LineNumber;

        var last = keywordLine - 1;
        if (last < 0 || !LayoutHelpers.IsBlankLine(text, last))
        {
            change = default;
            return false;
        }

        var first = last;
        while (first >= 1 && LayoutHelpers.IsBlankLine(text, first - 1))
        {
            first--;
        }

        var span = TextSpan.FromBounds(text.Lines[first].Start, text.Lines[last].EndIncludingLineBreak);
        change = new(span, string.Empty);
        return true;
    }
}
