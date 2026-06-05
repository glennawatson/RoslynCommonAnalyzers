// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a constructor summary with the standard "Initializes a new instance…" text (SST1642).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConstructorSummaryCodeFixProvider))]
[Shared]
public sealed class ConstructorSummaryCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.ConstructorStandardText.Id);

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
            var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
            if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } summary
                || XmlDocumentationHelper.DocumentedMember(summary) is not ConstructorDeclarationSyntax constructor
                || constructor.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } type)
            {
                continue;
            }

            var standardSummary = DocumentationConventions.ConstructorStandardSummary(type);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the standard constructor summary",
                    cancellationToken => ApplyAsync(context.Document, summary, standardSummary, cancellationToken),
                    equivalenceKey: nameof(ConstructorSummaryCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the summary element's content with the standard constructor text.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="summary">The summary element.</param>
    /// <param name="standardSummary">The standard summary inner text.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ApplyAsync(Document document, XmlElementSyntax summary, string standardSummary, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.Replace(summary.Span, "<summary>" + standardSummary + "</summary>"));
    }
}
