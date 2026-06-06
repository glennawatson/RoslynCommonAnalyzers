// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a blank line that sits next to a brace: after an opening brace (SST1505),
/// before a closing brace (SST1508), or before an opening brace (SST1509). The fix is
/// expressed as a minimal text change that deletes the offending physical line(s).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlankLineRemovalCodeFixProvider))]
[Shared]
public sealed class BlankLineRemovalCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.OpenBraceNotFollowedByBlankLine.Id,
        LayoutRules.CloseBraceNotPrecededByBlankLine.Id,
        LayoutRules.OpenBraceNotPrecededByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var after = diagnostic.Id == LayoutRules.OpenBraceNotFollowedByBlankLine.Id;
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove blank line",
                    cancellationToken => RemoveBlankLinesAsync(context.Document, diagnostic.Location.SourceSpan, after, cancellationToken),
                    equivalenceKey: nameof(BlankLineRemovalCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>Deletes the run of blank lines directly above or below the brace line.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="braceSpan">The span of the reported brace token.</param>
    /// <param name="after">When <see langword="true"/>, removes blank lines below the brace; otherwise above it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> RemoveBlankLinesAsync(Document document, TextSpan braceSpan, bool after, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var braceLine = text.Lines.GetLineFromPosition(braceSpan.Start).LineNumber;

        int first;
        int last;
        if (after)
        {
            first = braceLine + 1;
            if (!LayoutHelpers.IsBlankLine(text, first))
            {
                return document;
            }

            last = first;
            while (last + 1 < text.Lines.Count && LayoutHelpers.IsBlankLine(text, last + 1))
            {
                last++;
            }
        }
        else
        {
            last = braceLine - 1;
            if (!LayoutHelpers.IsBlankLine(text, last))
            {
                return document;
            }

            first = last;
            while (first >= 1 && LayoutHelpers.IsBlankLine(text, first - 1))
            {
                first--;
            }
        }

        var span = TextSpan.FromBounds(text.Lines[first].Start, text.Lines[last].EndIncludingLineBreak);
        return document.WithText(text.WithChanges(new TextChange(span, string.Empty)));
    }
}
