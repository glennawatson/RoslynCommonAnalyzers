// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Fixes the spacing around a standalone single-line comment: inserts a blank line before
/// a comment that lacks one (SST1515) and removes the blank line after a comment (SST1512).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SingleLineCommentSpacingCodeFixProvider))]
[Shared]
public sealed class SingleLineCommentSpacingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.SingleLineCommentPrecededByBlankLine.Id,
        LayoutRules.SingleLineCommentNotFollowedByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var insertBefore = diagnostic.Id == LayoutRules.SingleLineCommentPrecededByBlankLine.Id;
            context.RegisterCodeFix(
                CodeAction.Create(
                    insertBefore ? "Insert blank line before comment" : "Remove blank line after comment",
                    cancellationToken => FixAsync(context.Document, diagnostic.Location.SourceSpan, insertBefore, cancellationToken),
                    equivalenceKey: nameof(SingleLineCommentSpacingCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>Inserts or removes the blank line adjacent to the comment.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="commentSpan">The span of the reported comment.</param>
    /// <param name="insertBefore">When <see langword="true"/>, inserts a blank line above the comment; otherwise removes the blank line below it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> FixAsync(Document document, TextSpan commentSpan, bool insertBefore, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var commentLine = text.Lines.GetLineFromPosition(commentSpan.Start).LineNumber;

        if (insertBefore)
        {
            var position = text.Lines[commentLine].Start;
            return document.WithText(text.WithChanges(new TextChange(new TextSpan(position, 0), LayoutFixHelpers.DetectNewLine(text))));
        }

        var first = commentLine + 1;
        if (!LayoutHelpers.IsBlankLine(text, first))
        {
            return document;
        }

        var last = first;
        while (last + 1 < text.Lines.Count && LayoutHelpers.IsBlankLine(text, last + 1))
        {
            last++;
        }

        var span = TextSpan.FromBounds(text.Lines[first].Start, text.Lines[last].EndIncludingLineBreak);
        return document.WithText(text.WithChanges(new TextChange(span, string.Empty)));
    }
}
