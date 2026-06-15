// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Fixes the spacing around a standalone single-line comment: inserts a blank line before
/// a comment that lacks one (SST1515) and removes the blank line after a comment (SST1512).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SingleLineCommentSpacingCodeFixProvider))]
[Shared]
public sealed class SingleLineCommentSpacingCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.SingleLineCommentPrecededByBlankLine.Id,
        LayoutRules.SingleLineCommentNotFollowedByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

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

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var insertBefore = diagnostic.Id == LayoutRules.SingleLineCommentPrecededByBlankLine.Id;
        if (TryBuildChange(text, diagnostic.Location.SourceSpan, insertBefore) is not { } change)
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Inserts or removes the blank line adjacent to the comment.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="commentSpan">The span of the reported comment.</param>
    /// <param name="insertBefore">When <see langword="true"/>, inserts a blank line above the comment; otherwise removes the blank line below it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> FixAsync(Document document, TextSpan commentSpan, bool insertBefore, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return TryBuildChange(text, commentSpan, insertBefore) is { } change
            ? document.WithText(text.WithChanges(change))
            : document;
    }

    /// <summary>Builds the blank-line insert/remove change for a reported comment, if any is needed.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="commentSpan">The span of the reported comment.</param>
    /// <param name="insertBefore">When <see langword="true"/>, inserts a blank line above the comment; otherwise removes the blank line below it.</param>
    /// <returns>The text change to apply, or <see langword="null"/> when no change is required.</returns>
    private static TextChange? TryBuildChange(SourceText text, TextSpan commentSpan, bool insertBefore)
    {
        var commentLine = text.Lines.GetLineFromPosition(commentSpan.Start).LineNumber;

        if (insertBefore)
        {
            var position = text.Lines[commentLine].Start;
            return new TextChange(new(position, 0), LayoutFixHelpers.DetectNewLine(text));
        }

        var first = commentLine + 1;
        if (!LayoutHelpers.IsBlankLine(text, first))
        {
            return null;
        }

        var last = first;
        while (last + 1 < text.Lines.Count && LayoutHelpers.IsBlankLine(text, last + 1))
        {
            last++;
        }

        var span = TextSpan.FromBounds(text.Lines[first].Start, text.Lines[last].EndIncludingLineBreak);
        return new TextChange(span, string.Empty);
    }
}
