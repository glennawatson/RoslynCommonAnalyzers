// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Appends a terminal period to documentation prose that is missing one (SST1629).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DocumentationPeriodCodeFixProvider))]
[Shared]
public sealed class DocumentationPeriodCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.TextMustEndWithPeriod.Id);

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
            if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } element
                || !XmlDocumentationHelper.NeedsTerminalPeriod(element, out var insertPosition))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add a period",
                    cancellationToken => AddPeriodAsync(context.Document, insertPosition, cancellationToken),
                    equivalenceKey: nameof(DocumentationPeriodCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
        if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } element
            || !XmlDocumentationHelper.NeedsTerminalPeriod(element, out var insertPosition))
        {
            return;
        }

        changes.Add(BuildChange(insertPosition));
    }

    /// <summary>Inserts a period at <paramref name="position"/>.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="position">The source position to insert the period at.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> AddPeriodAsync(Document document, int position, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(BuildChange(position)));
    }

    /// <summary>Builds the change that inserts a period at <paramref name="position"/>.</summary>
    /// <param name="position">The source position to insert the period at.</param>
    /// <returns>The period-insertion text change.</returns>
    private static TextChange BuildChange(int position) => new(new(position, 0), ".");
}
