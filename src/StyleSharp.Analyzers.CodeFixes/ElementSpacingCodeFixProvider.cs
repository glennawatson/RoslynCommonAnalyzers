// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Inserts a blank line before a member that is not separated from the previous one (SST1516).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ElementSpacingCodeFixProvider))]
[Shared]
public sealed class ElementSpacingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.ElementsSeparatedByBlankLine.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Insert blank line",
                    cancellationToken => InsertBlankLineAsync(context.Document, member, cancellationToken),
                    equivalenceKey: nameof(ElementSpacingCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Inserts a blank line directly above the member's documentation header or first token.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="member">The member to separate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> InsertBlankLineAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var contentStartLine = LayoutHelpers.ContentStartLine(text, member);
        var position = text.Lines[contentStartLine].Start;
        return document.WithText(text.WithChanges(new TextChange(new(position, 0), newLine)));
    }
}
