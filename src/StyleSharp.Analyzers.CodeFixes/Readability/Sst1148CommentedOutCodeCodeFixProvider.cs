// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Deletes a reported commented-out line (SST1148). A comment that owns its line takes the line with it;
/// one that trails code loses only itself and the space ahead of it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1148CommentedOutCodeCodeFixProvider))]
[Shared]
public sealed class Sst1148CommentedOutCodeCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoCommentedOutCode.Id);

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
                    "Remove the commented-out code",
                    cancellationToken => RemoveAsync(context.Document, span, cancellationToken),
                    equivalenceKey: nameof(Sst1148CommentedOutCodeCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>Removes the reported comment, and its line when nothing else is on it.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="span">The comment's span.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> RemoveAsync(Document document, TextSpan span, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var line = text.Lines.GetLineFromPosition(span.Start);
        var before = text.ToString(TextSpan.FromBounds(line.Start, span.Start));
        var after = text.ToString(TextSpan.FromBounds(span.End, line.End));

        var removal = IsBlank(before) && IsBlank(after)
            ? TextSpan.FromBounds(line.Start, line.EndIncludingLineBreak)
            : TextSpan.FromBounds(span.Start - TrailingBlankLength(before), span.End);

        return document.WithText(text.WithChanges(new TextChange(removal, string.Empty)));
    }

    /// <summary>Returns whether a stretch of text is empty or whitespace.</summary>
    /// <param name="value">The text.</param>
    /// <returns><see langword="true"/> when nothing visible is in it.</returns>
    private static bool IsBlank(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Counts the whitespace at the end of a stretch of text.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The number of trailing whitespace characters.</returns>
    private static int TrailingBlankLength(string value)
    {
        var count = 0;
        for (var i = value.Length - 1; i >= 0 && char.IsWhiteSpace(value[i]); i--)
        {
            count++;
        }

        return count;
    }
}
