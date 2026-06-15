// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes the blank line before a chained <c>else</c>/<c>catch</c>/<c>finally</c> (SST1510)
/// or the <c>while</c> footer of a do/while loop (SST1511).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChainedBlockSpacingCodeFixProvider))]
[Shared]
public sealed class ChainedBlockSpacingCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.ChainedBlockNotPrecededByBlankLine.Id,
        LayoutRules.WhileFooterNotPrecededByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove blank line before keyword",
                    cancellationToken => RemoveAsync(context.Document, diagnostic.Location.SourceSpan, cancellationToken),
                    equivalenceKey: nameof(ChainedBlockSpacingCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryBuildChange(text, diagnostic.Location.SourceSpan, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Removes the run of blank lines directly above the reported keyword.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="keywordSpan">The span of the reported keyword.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> RemoveAsync(Document document, TextSpan keywordSpan, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return TryBuildChange(text, keywordSpan, out var change)
            ? document.WithText(text.WithChanges(change))
            : document;
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
        change = new TextChange(span, string.Empty);
        return true;
    }
}
