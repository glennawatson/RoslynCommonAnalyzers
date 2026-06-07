// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Inserts a blank line after a closing brace that is not followed by one (SST1513).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClosingBraceSpacingCodeFixProvider))]
[Shared]
public sealed class ClosingBraceSpacingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.CloseBraceFollowedByBlankLine.Id);

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
            if (!brace.IsKind(SyntaxKind.CloseBraceToken))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Insert blank line after closing brace",
                    cancellationToken => InsertBlankLineAsync(context.Document, brace, cancellationToken),
                    equivalenceKey: nameof(ClosingBraceSpacingCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Inserts a blank line before the token that follows the closing brace.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="brace">The closing brace token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> InsertBlankLineAsync(Document document, SyntaxToken brace, CancellationToken cancellationToken)
    {
        var next = brace.GetNextToken();
        if (next.IsKind(SyntaxKind.None))
        {
            return document;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var nextLine = text.Lines.GetLineFromPosition(next.SpanStart).LineNumber;
        var position = text.Lines[nextLine].Start;
        return document.WithText(text.WithChanges(new TextChange(new(position, 0), LayoutFixHelpers.DetectNewLine(text))));
    }
}
