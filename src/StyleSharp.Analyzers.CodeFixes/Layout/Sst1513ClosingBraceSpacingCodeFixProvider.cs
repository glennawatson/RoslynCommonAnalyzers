// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Inserts a blank line after a closing brace that is not followed by one (SST1513).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1513ClosingBraceSpacingCodeFixProvider))]
[Shared]
public sealed class Sst1513ClosingBraceSpacingCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.CloseBraceFollowedByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

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
            if (!brace.IsKind(SyntaxKind.CloseBraceToken))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Insert blank line after closing brace",
                    cancellationToken => InsertBlankLineAsync(context.Document, brace, cancellationToken),
                    equivalenceKey: nameof(Sst1513ClosingBraceSpacingCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var brace = root.FindToken(diagnostic.Location.SourceSpan.Start);
        if (!brace.IsKind(SyntaxKind.CloseBraceToken))
        {
            return;
        }

        if (!TryBuildChange(text, brace, out var change))
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
        if (!TryBuildChange(text, brace, out var change))
        {
            return document;
        }

        return document.WithText(text.WithChanges(change));
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
        change = new TextChange(new(position, 0), LayoutFixHelpers.DetectNewLine(text));
        return true;
    }
}
