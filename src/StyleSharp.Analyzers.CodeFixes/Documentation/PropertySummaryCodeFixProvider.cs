// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Prefixes a property summary with the accessor phrase ("Gets ", "Sets ", "Gets or sets ") (SST1623).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PropertySummaryCodeFixProvider))]
[Shared]
public sealed class PropertySummaryCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.PropertySummaryAccessors.Id);

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
                || XmlDocumentationHelper.DocumentedMember(summary) is not PropertyDeclarationSyntax property)
            {
                continue;
            }

            var prefix = DocumentationConventions.PropertyAccessorPrefix(property);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Prefix summary with '" + prefix.TrimEnd() + "'",
                    cancellationToken => ApplyAsync(context.Document, summary, prefix, cancellationToken),
                    equivalenceKey: nameof(PropertySummaryCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Inserts the accessor prefix and lower-cases the first existing word.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="summary">The summary element.</param>
    /// <param name="prefix">The accessor prefix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ApplyAsync(Document document, XmlElementSyntax summary, string prefix, CancellationToken cancellationToken)
    {
        if (!XmlDocumentationHelper.TryGetFirstTextCharacter(summary, out var first, out var position))
        {
            return document;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var replacement = prefix + char.ToLowerInvariant(first);
        return document.WithText(text.WithChanges(new TextChange(new(position, 1), replacement)));
    }
}
