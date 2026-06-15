// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Moves a reported opening parenthesis or bracket back onto the declaration line (SST1110).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OpeningParenOnDeclarationLineCodeFixProvider))]
[Shared]
public sealed class OpeningParenOnDeclarationLineCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.OpeningParenOnDeclarationLine.Id);

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

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (BuildChange(text, root, diagnostic.Location.SourceSpan) is not { } change)
        {
            return;
        }

        changes.Add(change);
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

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (BuildChange(text, root, openingSpan) is not { } change)
        {
            return document;
        }

        return document.WithText(text.WithChanges(change));
    }

    /// <summary>Builds the change that removes the whitespace before the opening token, if any applies.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="openingSpan">The diagnostic span on the opening token.</param>
    /// <returns>The text change to apply, or <see langword="null"/> when the fix does not apply.</returns>
    private static TextChange? BuildChange(SourceText text, SyntaxNode root, TextSpan openingSpan)
    {
        var openingToken = root.FindToken(openingSpan.Start);
        if (!IsSupportedOpeningToken(openingToken))
        {
            return null;
        }

        var previousToken = openingToken.GetPreviousToken();
        if (previousToken.IsKind(SyntaxKind.None))
        {
            return null;
        }

        if (!LayoutFixHelpers.IsWhitespaceBetween(text, previousToken.Span.End, openingToken.SpanStart))
        {
            return null;
        }

        return new TextChange(TextSpan.FromBounds(previousToken.Span.End, openingToken.SpanStart), string.Empty);
    }

    /// <summary>Returns whether the token is an opening parenthesis or bracket handled by SST1110.</summary>
    /// <param name="token">The token to inspect.</param>
    /// <returns><see langword="true"/> when the token is supported.</returns>
    private static bool IsSupportedOpeningToken(SyntaxToken token)
        => token.IsKind(SyntaxKind.OpenParenToken) || token.IsKind(SyntaxKind.OpenBracketToken);
}
