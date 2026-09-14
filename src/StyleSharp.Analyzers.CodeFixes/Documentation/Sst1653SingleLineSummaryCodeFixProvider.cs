// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Collapses a short multi-line <c>&lt;summary&gt;</c> onto a single line (SST1653).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1653SingleLineSummaryCodeFixProvider))]
[Shared]
public sealed class Sst1653SingleLineSummaryCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.SingleLineSummary.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Put summary on a single line",
            nameof(Sst1653SingleLineSummaryCodeFixProvider),
            DocumentationElementFix.FindElement,
            CollapseAsync);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (DocumentationElementFix.FindElement(root, diagnostic) is not { } summary)
        {
            return;
        }

        changes.Add(BuildChange(text, summary));
    }

    /// <summary>Rewrites the summary element's text so the tags and content sit on one line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="summary">The summary element to collapse.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> CollapseAsync(Document document, XmlElementSyntax summary, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(BuildChange(text, summary)));
    }

    /// <summary>Builds the single-line replacement change for a summary element.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="summary">The summary element to collapse.</param>
    /// <returns>The text change that collapses the summary onto one line.</returns>
    private static TextChange BuildChange(SourceText text, XmlElementSyntax summary)
    {
        var innerSpan = TextSpan.FromBounds(summary.StartTag.Span.End, summary.EndTag.Span.Start);
        return DocumentationElementFix.ReplaceSummary(summary, SummaryCollapse.Collapse(text, innerSpan));
    }
}
