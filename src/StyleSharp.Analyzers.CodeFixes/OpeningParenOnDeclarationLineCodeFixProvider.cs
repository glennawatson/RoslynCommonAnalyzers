// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Moves a reported opening parenthesis or bracket back onto the declaration line (SST1110).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OpeningParenOnDeclarationLineCodeFixProvider))]
[Shared]
public sealed class OpeningParenOnDeclarationLineCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.OpeningParenOnDeclarationLine.Id);

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
            var openingToken = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (!IsSupportedOpeningToken(openingToken))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Place the opening parenthesis or bracket on the declaration line",
                    cancellationToken => FixAsync(context.Document, diagnostic.Location.SourceSpan, cancellationToken),
                    equivalenceKey: nameof(OpeningParenOnDeclarationLineCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Removes the intervening whitespace between the declaration token and the opening token.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="openingSpan">The diagnostic span on the opening token.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> FixAsync(Document document, TextSpan openingSpan, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var openingToken = root.FindToken(openingSpan.Start);
        if (!IsSupportedOpeningToken(openingToken))
        {
            return document;
        }

        var previousToken = openingToken.GetPreviousToken();
        if (previousToken.IsKind(SyntaxKind.None))
        {
            return document;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (!LayoutFixHelpers.IsWhitespaceBetween(text, previousToken.Span.End, openingToken.SpanStart))
        {
            return document;
        }

        var change = new TextChange(TextSpan.FromBounds(previousToken.Span.End, openingToken.SpanStart), string.Empty);
        return document.WithText(text.WithChanges(change));
    }

    /// <summary>Returns whether the token is an opening parenthesis or bracket handled by SST1110.</summary>
    /// <param name="token">The token to inspect.</param>
    /// <returns><see langword="true"/> when the token is supported.</returns>
    private static bool IsSupportedOpeningToken(SyntaxToken token)
        => token.IsKind(SyntaxKind.OpenParenToken) || token.IsKind(SyntaxKind.OpenBracketToken);
}
