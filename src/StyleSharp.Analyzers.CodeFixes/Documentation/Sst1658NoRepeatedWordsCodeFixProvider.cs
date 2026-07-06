// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Removes the second occurrence of a word typed twice in a row in documentation text (SST1658).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1658NoRepeatedWordsCodeFixProvider))]
[Shared]
public sealed class Sst1658NoRepeatedWordsCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.NoRepeatedWords.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in context.Diagnostics)
        {
            var wordSpan = diagnostic.Location.SourceSpan;
            if (!TryGetRemovalChange(text, wordSpan, out _))
            {
                // No clean removal exists; the diagnostic stays reported without a fix.
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the repeated word",
                    cancellationToken => RemoveRepeatedWordAsync(context.Document, wordSpan, cancellationToken),
                    equivalenceKey: nameof(Sst1658NoRepeatedWordsCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryGetRemovalChange(text, diagnostic.Location.SourceSpan, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Removes the repeated word reported at <paramref name="wordSpan"/>.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="wordSpan">The span of the repeated word's second occurrence.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> RemoveRepeatedWordAsync(Document document, TextSpan wordSpan, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return TryGetRemovalChange(text, wordSpan, out var change) ? document.WithText(text.WithChanges(change)) : document;
    }

    /// <summary>
    /// Builds the removal for one repeated word. When the first occurrence sits on the same line the
    /// word and the whitespace run before it are removed; when the word opens a new documentation
    /// line the word and one following space are removed so the line break and exterior survive.
    /// </summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="wordSpan">The span of the repeated word's second occurrence.</param>
    /// <param name="change">The removal change when one is achievable.</param>
    /// <returns><see langword="true"/> when a clean removal exists.</returns>
    private static bool TryGetRemovalChange(SourceText text, TextSpan wordSpan, out TextChange change)
    {
        change = default;
        if (wordSpan.Length == 0 || wordSpan.End > text.Length)
        {
            return false;
        }

        var whitespaceStart = FindWhitespaceRunStart(text, wordSpan.Start);
        var before = whitespaceStart > 0 ? text[whitespaceStart - 1] : '\0';

        // Both occurrences sit on the same line: drop the word and the whitespace before it.
        if (whitespaceStart < wordSpan.Start && char.IsLetter(before))
        {
            change = new TextChange(TextSpan.FromBounds(whitespaceStart, wordSpan.End), string.Empty);
            return true;
        }

        // The word is the first on its documentation line (behind the exterior or a raw line
        // break): drop the word and one following space, keeping the line break.
        if (!IsLineOpeningBoundary(before))
        {
            return false;
        }

        change = new TextChange(TextSpan.FromBounds(wordSpan.Start, FindRemovalEnd(text, wordSpan.End)), string.Empty);
        return true;
    }

    /// <summary>Walks backwards over the space/tab run that precedes a position.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="position">The position to walk back from.</param>
    /// <returns>The start of the whitespace run, or the position itself when none precedes it.</returns>
    private static int FindWhitespaceRunStart(SourceText text, int position)
    {
        var whitespaceStart = position;
        while (whitespaceStart > 0 && IsSpaceOrTab(text[whitespaceStart - 1]))
        {
            whitespaceStart--;
        }

        return whitespaceStart;
    }

    /// <summary>Returns whether the character before the word opens a new documentation line.</summary>
    /// <param name="before">The character preceding the word and its whitespace run.</param>
    /// <returns><see langword="true"/> for a comment exterior character or a raw line break.</returns>
    private static bool IsLineOpeningBoundary(char before) => before is '/' or '*' or '\n' or '\r';

    /// <summary>Extends the removal end over one following space, when present.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="end">The word's end position.</param>
    /// <returns>The removal end position.</returns>
    private static int FindRemovalEnd(SourceText text, int end)
        => end < text.Length && text[end] == ' ' ? end + 1 : end;

    /// <summary>Returns whether a character is a same-line whitespace character.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for a space or tab.</returns>
    private static bool IsSpaceOrTab(char character) => character is ' ' or '\t';
}
