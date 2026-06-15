// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Fixes documentation-header spacing: inserts a blank line before a header that lacks one
/// (SST1514) and removes the blank line between a header and its element (SST1506).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DocumentationHeaderSpacingCodeFixProvider))]
[Shared]
public sealed class DocumentationHeaderSpacingCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.DocHeaderPrecededByBlankLine.Id,
        LayoutRules.DocHeaderNotFollowedByBlankLine.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member
                || !LayoutHelpers.TryGetDocHeader(member, out _))
            {
                continue;
            }

            var insertBefore = diagnostic.Id == LayoutRules.DocHeaderPrecededByBlankLine.Id;
            context.RegisterCodeFix(
                CodeAction.Create(
                    insertBefore ? "Insert blank line before documentation" : "Remove blank line after documentation",
                    cancellationToken => FixAsync(context.Document, member, insertBefore, cancellationToken),
                    equivalenceKey: nameof(DocumentationHeaderSpacingCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member
            || !LayoutHelpers.TryGetDocHeader(member, out _))
        {
            return;
        }

        var insertBefore = diagnostic.Id == LayoutRules.DocHeaderPrecededByBlankLine.Id;
        changes.Add(BuildChange(text, member, insertBefore));
    }

    /// <summary>Applies the blank-line insertion or removal around the member's documentation header.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="member">The documented member.</param>
    /// <param name="insertBefore">When <see langword="true"/>, inserts a blank line before the header; otherwise removes the blank line after it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> FixAsync(Document document, MemberDeclarationSyntax member, bool insertBefore, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(BuildChange(text, member, insertBefore)));
    }

    /// <summary>Builds the change that inserts or removes the blank line around the member's documentation header.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="member">The documented member.</param>
    /// <param name="insertBefore">When <see langword="true"/>, inserts a blank line before the header; otherwise removes the blank line after it.</param>
    /// <returns>The text change to apply.</returns>
    private static TextChange BuildChange(SourceText text, MemberDeclarationSyntax member, bool insertBefore)
    {
        LayoutHelpers.TryGetDocHeader(member, out var header);

        if (insertBefore)
        {
            var headerFirstLine = LayoutHelpers.LineOf(text, header.SpanStart);
            var position = text.Lines[headerFirstLine].Start;
            return new TextChange(new(position, 0), LayoutFixHelpers.DetectNewLine(text));
        }

        var headerLastLine = LayoutHelpers.LineOf(text, header.Span.End - 1);
        var memberLine = LayoutHelpers.StartLine(text, member.GetFirstToken());
        var span = TextSpan.FromBounds(text.Lines[headerLastLine + 1].Start, text.Lines[memberLine].Start);
        return new TextChange(span, string.Empty);
    }
}
