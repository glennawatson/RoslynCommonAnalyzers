// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes the blank line before a chained <c>else</c>/<c>catch</c>/<c>finally</c> (SST1510)
/// or the <c>while</c> footer of a do/while loop (SST1511).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChainedBlockSpacingCodeFixProvider))]
[Shared]
public sealed class ChainedBlockSpacingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.ChainedBlockNotPrecededByBlankLine.Id,
        LayoutRules.WhileFooterNotPrecededByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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

    /// <summary>Removes the run of blank lines directly above the reported keyword.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="keywordSpan">The span of the reported keyword.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> RemoveAsync(Document document, TextSpan keywordSpan, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var keywordLine = text.Lines.GetLineFromPosition(keywordSpan.Start).LineNumber;

        var last = keywordLine - 1;
        if (last < 0 || !LayoutHelpers.IsBlankLine(text, last))
        {
            return document;
        }

        var first = last;
        while (first >= 1 && LayoutHelpers.IsBlankLine(text, first - 1))
        {
            first--;
        }

        var span = TextSpan.FromBounds(text.Lines[first].Start, text.Lines[last].EndIncludingLineBreak);
        return document.WithText(text.WithChanges(new TextChange(span, string.Empty)));
    }
}
