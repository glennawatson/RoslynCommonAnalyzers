// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves a brace that shares its line with other code (SST1500) onto its own line,
/// inserting the appropriate indentation. The rewrite is a set of minimal text changes
/// that break the line before and/or after the brace; it is skipped when a comment sits
/// between the brace and the code it shares a line with.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BracePlacementCodeFixProvider))]
[Shared]
public sealed class BracePlacementCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.BracesOnOwnLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var brace = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (!brace.IsKind(SyntaxKind.OpenBraceToken) && !brace.IsKind(SyntaxKind.CloseBraceToken))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Place brace on its own line",
                    cancellationToken => PlaceOnOwnLineAsync(context.Document, brace, cancellationToken),
                    equivalenceKey: nameof(BracePlacementCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Builds the text changes that move the brace and its line-mates onto separate lines.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="brace">The reported brace token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> PlaceOnOwnLineAsync(Document document, SyntaxToken brace, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var braceLine = text.Lines.GetLineFromPosition(brace.SpanStart).LineNumber;
        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var changes = new List<TextChange>(2);

        AddLeadingBreak(text, brace, braceLine, newLine, changes);
        AddTrailingBreak(text, brace, braceLine, newLine, changes);

        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Returns the indent the brace should sit at on its own line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="brace">The brace token.</param>
    /// <returns>The owner indentation: the opening brace's line for a closing brace, otherwise the brace's own line.</returns>
    private static string OwnerIndent(SourceText text, SyntaxToken brace)
        => brace.IsKind(SyntaxKind.CloseBraceToken) && LayoutHelpers.TryGetBraces(brace.Parent!, out var open, out _)
            ? LayoutFixHelpers.IndentOfLine(text, open.SpanStart)
            : LayoutFixHelpers.IndentOfLine(text, brace.SpanStart);

    /// <summary>Adds the change that breaks the line before the brace, when the brace shares it with earlier code.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="brace">The brace token.</param>
    /// <param name="braceLine">The line the brace is on.</param>
    /// <param name="newLine">The document newline sequence.</param>
    /// <param name="changes">The change set to append to.</param>
    private static void AddLeadingBreak(SourceText text, SyntaxToken brace, int braceLine, string newLine, List<TextChange> changes)
    {
        var previous = brace.GetPreviousToken();
        if (previous.IsKind(SyntaxKind.None)
            || text.Lines.GetLineFromPosition(previous.Span.End - 1).LineNumber != braceLine
            || !LayoutFixHelpers.IsWhitespaceBetween(text, previous.Span.End, brace.SpanStart))
        {
            return;
        }

        changes.Add(new(TextSpan.FromBounds(previous.Span.End, brace.SpanStart), newLine + OwnerIndent(text, brace)));
    }

    /// <summary>Adds the change that breaks the line after an opening brace, when code follows it on the same line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="brace">The brace token.</param>
    /// <param name="braceLine">The line the brace is on.</param>
    /// <param name="newLine">The document newline sequence.</param>
    /// <param name="changes">The change set to append to.</param>
    private static void AddTrailingBreak(SourceText text, SyntaxToken brace, int braceLine, string newLine, List<TextChange> changes)
    {
        if (!brace.IsKind(SyntaxKind.OpenBraceToken))
        {
            return;
        }

        var next = brace.GetNextToken();
        if (next.IsKind(SyntaxKind.None)
            || text.Lines.GetLineFromPosition(next.SpanStart).LineNumber != braceLine
            || !LayoutFixHelpers.IsWhitespaceBetween(text, brace.Span.End, next.SpanStart))
        {
            return;
        }

        changes.Add(new(TextSpan.FromBounds(brace.Span.End, next.SpanStart), newLine + LayoutFixHelpers.IndentOfLine(text, brace.SpanStart) + LayoutFixHelpers.IndentStep));
    }
}
