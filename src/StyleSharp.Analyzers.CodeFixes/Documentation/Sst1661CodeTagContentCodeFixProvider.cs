// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Swaps a documentation code tag so it matches its content (SST1661): <c>&lt;code&gt;</c> becomes
/// <c>&lt;c&gt;</c> for a single-line snippet, and <c>&lt;c&gt;</c> becomes <c>&lt;code&gt;</c> for a
/// multi-line one. Both the start and end tag names are renamed.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1661CodeTagContentCodeFixProvider))]
[Shared]
public sealed class Sst1661CodeTagContentCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DocumentationRules.CodeTagContent.Id);

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
            if (!TryGetSwap(root, diagnostic, out _, out var target))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use the '<{target}>' tag",
                    cancellationToken => SwapAsync(context.Document, diagnostic, cancellationToken),
                    equivalenceKey: nameof(Sst1661CodeTagContentCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryGetSwap(root, diagnostic, out var element, out var target))
        {
            return;
        }

        changes.Add(new TextChange(element.StartTag.Name.Span, target));
        changes.Add(new TextChange(element.EndTag.Name.Span, target));
    }

    /// <summary>Applies the tag-name swap to the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> SwapAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || !TryGetSwap(root, diagnostic, out var element, out var target))
        {
            return document;
        }

        return document.WithText(text.WithChanges(
            new TextChange(element.StartTag.Name.Span, target),
            new TextChange(element.EndTag.Name.Span, target)));
    }

    /// <summary>Resolves the element to rename and the tag name to rename it to.</summary>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="element">The <c>&lt;c&gt;</c>/<c>&lt;code&gt;</c> element when found.</param>
    /// <param name="target">The tag name to swap to when found.</param>
    /// <returns><see langword="true"/> when both were resolved.</returns>
    private static bool TryGetSwap(SyntaxNode root, Diagnostic diagnostic, out XmlElementSyntax element, out string target)
    {
        element = null!;
        target = string.Empty;

        var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
        if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } found
            || !diagnostic.Properties.TryGetValue(Sst1661CodeTagContentAnalyzer.TargetTagKey, out var tag)
            || string.IsNullOrEmpty(tag))
        {
            return false;
        }

        element = found;
        target = tag!;
        return true;
    }
}
