// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a destructor summary with the standard "Finalizes an instance…" text (SST1643).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DestructorSummaryCodeFixProvider))]
[Shared]
public sealed class DestructorSummaryCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.DestructorStandardText.Id);

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
            var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
            if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } summary
                || XmlDocumentationHelper.DocumentedMember(summary) is not DestructorDeclarationSyntax destructor
                || destructor.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } type)
            {
                continue;
            }

            var standardSummary = DocumentationConventions.DestructorStandardSummary(type);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the standard destructor summary",
                    cancellationToken => ApplyAsync(context.Document, summary, standardSummary, cancellationToken),
                    equivalenceKey: nameof(DestructorSummaryCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
        if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } summary
            || XmlDocumentationHelper.DocumentedMember(summary) is not DestructorDeclarationSyntax destructor
            || destructor.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } type)
        {
            return;
        }

        changes.Add(BuildChange(summary, DocumentationConventions.DestructorStandardSummary(type)));
    }

    /// <summary>Replaces the summary element's content with the standard destructor text.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="summary">The summary element.</param>
    /// <param name="standardSummary">The standard summary inner text.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ApplyAsync(Document document, XmlElementSyntax summary, string standardSummary, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(BuildChange(summary, standardSummary)));
    }

    /// <summary>Builds the replacement change that swaps in the standard summary text.</summary>
    /// <param name="summary">The summary element.</param>
    /// <param name="standardSummary">The standard summary inner text.</param>
    /// <returns>The text change that rewrites the summary.</returns>
    private static TextChange BuildChange(XmlElementSyntax summary, string standardSummary)
        => new(summary.Span, "<summary>" + standardSummary + "</summary>");
}
