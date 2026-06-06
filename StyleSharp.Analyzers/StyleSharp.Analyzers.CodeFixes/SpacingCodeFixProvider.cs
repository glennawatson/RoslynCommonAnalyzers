// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Fixes the trivia spacing rules by editing the source text directly: removes trailing
/// whitespace (SST1028), collapses multiple whitespace to a single space (SST1025), replaces
/// tabs with spaces (SST1027), and inserts the missing space after <c>//</c> (SST1005).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SpacingCodeFixProvider))]
[Shared]
public sealed class SpacingCodeFixProvider : CodeFixProvider
{
    /// <summary>The number of spaces a tab is replaced with (the repository uses four-space indents).</summary>
    private const int TabWidth = 4;

    /// <summary>The width of the comment opener.</summary>
    private const int CommentOpenerLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        SpacingRules.CommentBeginsWithSpace.Id,
        SpacingRules.DocumentationBeginsWithSpace.Id,
        SpacingRules.PreprocessorKeywordSpacing.Id,
        SpacingRules.MultipleWhitespace.Id,
        SpacingRules.UseSpacesNotTabs.Id,
        SpacingRules.NoTrailingWhitespace.Id,
        SpacingRules.CommaSpacing.Id,
        SpacingRules.SemicolonSpacing.Id,
        SpacingRules.OpeningGenericBracket.Id,
        SpacingRules.ClosingGenericBracket.Id,
        SpacingRules.OpeningAttributeBracket.Id,
        SpacingRules.ClosingAttributeBracket.Id,
        SpacingRules.NullableSpacing.Id,
        SpacingRules.MemberAccessSpacing.Id,
        SpacingRules.ImplicitArraySpacing.Id,
        SpacingRules.OperatorKeywordSpacing.Id,
        SpacingRules.ClosingSquareBracket.Id,
        SpacingRules.IncrementDecrementSpacing.Id,
        SpacingRules.NegativeSignSpacing.Id,
        SpacingRules.PositiveSignSpacing.Id,
        SpacingRules.KeywordSpacing.Id,
        SpacingRules.OperatorSpacing.Id,
        SpacingRules.OpeningParenthesis.Id,
        SpacingRules.ClosingParenthesis.Id,
        SpacingRules.OpeningBrace.Id,
        SpacingRules.ClosingBrace.Id,
        SpacingRules.ColonSpacing.Id,
        SpacingRules.PointerSymbolSpacing.Id,
        SpacingRules.OpeningSquareBracket.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            diagnostic.Properties.TryGetValue(SpacingAnalyzer.ActionKey, out var action);
            var id = diagnostic.Id;
            var span = diagnostic.Location.SourceSpan;
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Fix spacing",
                    cancellationToken => FixAsync(context.Document, id, span, action, cancellationToken),
                    equivalenceKey: nameof(SpacingCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>Applies the text change for the reported spacing diagnostic.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="span">The reported span.</param>
    /// <param name="action">The stashed punctuation fix action, when present.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> FixAsync(Document document, string id, TextSpan span, string? action, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var change = action is null ? TriviaChange(id, span, text) : PunctuationChange(action, span, text);
        return document.WithText(text.WithChanges(change));
    }

    /// <summary>Computes the text change for a trivia spacing diagnostic.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="span">The reported span.</param>
    /// <param name="text">The source text.</param>
    /// <returns>The text change to apply.</returns>
    private static TextChange TriviaChange(string id, TextSpan span, SourceText text)
    {
        if (id == SpacingRules.NoTrailingWhitespace.Id)
        {
            return new TextChange(span, string.Empty);
        }

        if (id == SpacingRules.MultipleWhitespace.Id)
        {
            return new TextChange(span, " ");
        }

        if (id == SpacingRules.UseSpacesNotTabs.Id)
        {
            return new TextChange(span, text.ToString(span).Replace("\t", new string(' ', TabWidth)));
        }

        var insertAt = span.Start + CommentOpenerLength;
        return new TextChange(new TextSpan(insertAt, 0), " ");
    }

    /// <summary>Computes the text change for a comma/semicolon spacing diagnostic.</summary>
    /// <param name="action">The fix action.</param>
    /// <param name="span">The punctuation token span.</param>
    /// <param name="text">The source text.</param>
    /// <returns>The text change to apply.</returns>
    private static TextChange PunctuationChange(string action, TextSpan span, SourceText text)
    {
        if (action == SpacingAnalyzer.AddAfter)
        {
            return new TextChange(new TextSpan(span.End, 0), " ");
        }

        if (action == SpacingAnalyzer.AddBefore)
        {
            return new TextChange(new TextSpan(span.Start, 0), " ");
        }

        if (action == SpacingAnalyzer.RemoveAfter)
        {
            var afterEnd = span.End;
            while (afterEnd < text.Length && (text[afterEnd] == ' ' || text[afterEnd] == '\t'))
            {
                afterEnd++;
            }

            return new TextChange(TextSpan.FromBounds(span.End, afterEnd), string.Empty);
        }

        var start = span.Start;
        while (start > 0 && (text[start - 1] == ' ' || text[start - 1] == '\t'))
        {
            start--;
        }

        return new TextChange(TextSpan.FromBounds(start, span.Start), string.Empty);
    }
}
