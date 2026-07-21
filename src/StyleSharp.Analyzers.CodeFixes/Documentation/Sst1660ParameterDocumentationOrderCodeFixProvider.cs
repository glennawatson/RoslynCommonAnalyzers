// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reorders a member's <c>&lt;param&gt;</c> elements so they match the parameter declaration order (SST1660).
/// Only the element text moves between the fixed <c>///</c> exteriors, so the surrounding layout is preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1660ParameterDocumentationOrderCodeFixProvider))]
[Shared]
public sealed class Sst1660ParameterDocumentationOrderCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DocumentationRules.ParameterDocumentationOrder.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var changes = new List<TextChange>();
            BuildChanges(text, root, diagnostic, changes);
            if (changes.Count == 0)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Order <param> elements to match the parameters",
                    cancellationToken => ReorderAsync(context.Document, changes, cancellationToken),
                    equivalenceKey: nameof(Sst1660ParameterDocumentationOrderCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => BuildChanges(text, root, diagnostic, changes);

    /// <summary>Applies the reordering text changes to the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="changes">The reordering text changes.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ReorderAsync(Document document, List<TextChange> changes, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(changes));
    }

    /// <summary>Computes the changes that swap each <c>&lt;param&gt;</c> element's text into its declaration-order slot.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The accumulating list of text changes.</param>
    private static void BuildChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
        if (node.FirstAncestorOrSelf<DocumentationCommentTriviaSyntax>() is not { } documentation
            || XmlDocumentationHelper.DocumentedMember(node) is not { } member)
        {
            return;
        }

        var parameters = DocumentedParameterList.Of(member);
        if (parameters.Count < 2)
        {
            return;
        }

        var elements = new List<XmlNodeSyntax>(parameters.Count);
        var names = new List<string>(parameters.Count);
        if (!TryGatherParamElements(documentation, elements, names) || elements.Count != parameters.Count)
        {
            return;
        }

        for (var slot = 0; slot < parameters.Count; slot++)
        {
            var sourceIndex = names.IndexOf(parameters[slot].Identifier.ValueText);
            if (sourceIndex < 0)
            {
                // The documented set is not an exact match; leave it alone.
                return;
            }

            if (sourceIndex != slot)
            {
                changes.Add(new TextChange(elements[slot].Span, text.ToString(elements[sourceIndex].Span)));
            }
        }
    }

    /// <summary>Collects the <c>&lt;param&gt;</c> elements and their names in document order.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="elements">The list receiving the <c>&lt;param&gt;</c> elements.</param>
    /// <param name="names">The list receiving each element's name.</param>
    /// <returns><see langword="false"/> when a <c>&lt;param&gt;</c> lacks a name attribute.</returns>
    private static bool TryGatherParamElements(DocumentationCommentTriviaSyntax documentation, List<XmlNodeSyntax> elements, List<string> names)
    {
        foreach (var content in documentation.Content)
        {
            if (XmlDocumentationHelper.GetElementName(content) != "param")
            {
                continue;
            }

            var name = XmlDocumentationHelper.NameAttribute(content);
            if (name is null)
            {
                return false;
            }

            elements.Add(content);
            names.Add(name);
        }

        return true;
    }
}
