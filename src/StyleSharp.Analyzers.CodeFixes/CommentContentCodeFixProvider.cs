// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an empty comment (SST1120). When the comment is the only content on its line the
/// whole line is removed; when it trails code only the comment and the whitespace before it
/// are removed.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CommentContentCodeFixProvider))]
[Shared]
public sealed class CommentContentCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.CommentMustContainText.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var span = diagnostic.Location.SourceSpan;
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove empty comment",
                    cancellationToken => RemoveAsync(context.Document, span, cancellationToken),
                    equivalenceKey: nameof(CommentContentCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>Computes and applies the removal of the empty comment.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="span">The comment span.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> RemoveAsync(Document document, TextSpan span, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var removal = ComputeRemoval(text, span);
        return document.WithText(text.WithChanges(new TextChange(removal, string.Empty)));
    }

    /// <summary>Computes the span to delete for an empty comment.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="span">The comment span.</param>
    /// <returns>The span to remove — the whole line when the comment owns it, otherwise the comment and its leading whitespace.</returns>
    private static TextSpan ComputeRemoval(SourceText text, TextSpan span)
    {
        var lineStart = FindLineStart(text, span.Start);
        var lineEnd = FindLineEnd(text, span.End);

        if (IsWhitespaceRange(text, lineStart, span.Start) && IsWhitespaceRange(text, span.End, lineEnd))
        {
            // The comment owns the line: drop the line, including its terminating newline.
            return TextSpan.FromBounds(lineStart, lineEnd < text.Length ? lineEnd + 1 : lineEnd);
        }

        // The comment trails code: drop it and the whitespace separating it from the code.
        return TextSpan.FromBounds(TrimWhitespaceBack(text, lineStart, span.Start), span.End);
    }

    /// <summary>Returns the position just after the previous newline (the start of the line).</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The position to scan back from.</param>
    /// <returns>The start of the line containing the position.</returns>
    private static int FindLineStart(SourceText text, int position)
    {
        while (position > 0 && text[position - 1] != '\n')
        {
            position--;
        }

        return position;
    }

    /// <summary>Returns the position of the next newline, or the text length (the end of the line).</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The position to scan forward from.</param>
    /// <returns>The end of the line containing the position.</returns>
    private static int FindLineEnd(SourceText text, int position)
    {
        while (position < text.Length && text[position] != '\n')
        {
            position++;
        }

        return position;
    }

    /// <summary>Scans back over space and tab characters from a position, bounded by the line start.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="lineStart">The lower bound for the scan.</param>
    /// <param name="position">The position to scan back from.</param>
    /// <returns>The first position that is not preceded by trailing whitespace.</returns>
    private static int TrimWhitespaceBack(SourceText text, int lineStart, int position)
    {
        while (position > lineStart && (text[position - 1] == ' ' || text[position - 1] == '\t'))
        {
            position--;
        }

        return position;
    }

    /// <summary>Returns whether the half-open range is empty or all whitespace.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <returns><see langword="true"/> when no non-whitespace character is present.</returns>
    private static bool IsWhitespaceRange(SourceText text, int start, int end)
    {
        for (var position = start; position < end; position++)
        {
            if (!char.IsWhiteSpace(text[position]))
            {
                return false;
            }
        }

        return true;
    }
}
