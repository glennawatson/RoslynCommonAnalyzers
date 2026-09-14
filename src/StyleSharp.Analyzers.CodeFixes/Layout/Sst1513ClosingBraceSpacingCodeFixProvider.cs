// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Inserts a blank line after a closing brace that is not followed by one (SST1513).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1513ClosingBraceSpacingCodeFixProvider))]
[Shared]
public sealed class Sst1513ClosingBraceSpacingCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.CloseBraceFollowedByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync<SyntaxToken>(
            context,
            "Insert blank line after closing brace",
            nameof(Sst1513ClosingBraceSpacingCodeFixProvider),
            TryFindClosingBrace,
            InsertBlankLineAsync);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryFindClosingBrace(root, diagnostic, out var brace) || !TryBuildChange(text, brace, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Inserts a blank line before the token that follows the closing brace.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="brace">The closing brace token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> InsertBlankLineAsync(Document document, SyntaxToken brace, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return !TryBuildChange(text, brace, out var change) ? document : document.WithText(text.WithChanges(change));
    }

    /// <summary>Builds the blank-line insertion before the token that follows the closing brace.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="brace">The closing brace token.</param>
    /// <param name="change">The computed text change, when one applies.</param>
    /// <returns><see langword="true"/> when a change was produced.</returns>
    private static bool TryBuildChange(SourceText text, SyntaxToken brace, out TextChange change)
    {
        var next = brace.GetNextToken();
        if (next.IsKind(SyntaxKind.None))
        {
            change = default;
            return false;
        }

        var nextLine = text.Lines.GetLineFromPosition(next.SpanStart).LineNumber;
        var position = text.Lines[nextLine].Start;
        change = new(new(position, 0), LayoutFixHelpers.DetectNewLine(text));
        return true;
    }

    /// <summary>Finds the closing brace a diagnostic reports.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="brace">The token at the diagnostic's start.</param>
    /// <returns><see langword="true"/> when that token is a closing brace.</returns>
    private static bool TryFindClosingBrace(SyntaxNode root, Diagnostic diagnostic, out SyntaxToken brace)
    {
        brace = root.FindToken(diagnostic.Location.SourceSpan.Start);
        return brace.IsKind(SyntaxKind.CloseBraceToken);
    }
}
