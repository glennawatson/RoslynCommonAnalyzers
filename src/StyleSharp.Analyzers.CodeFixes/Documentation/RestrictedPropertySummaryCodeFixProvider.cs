// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Replaces "Gets or sets" with "Gets" for SST1624.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RestrictedPropertySummaryCodeFixProvider))]
[Shared]
public sealed class RestrictedPropertySummaryCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <summary>The phrase removed by the fix.</summary>
    private const string ExistingPrefix = "Gets or sets";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.PropertySummaryOmitsRestrictedSetter.Id);

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

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
            if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } summary)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Describe the readable accessor only",
                    cancellationToken => ApplyAsync(context.Document, summary, cancellationToken),
                    equivalenceKey: nameof(RestrictedPropertySummaryCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
        if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } summary)
        {
            return;
        }

        if (!TryBuildChange(summary, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Replaces the existing accessor phrase in the first XML text token.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="summary">The summary element.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ApplyAsync(Document document, XmlElementSyntax summary, CancellationToken cancellationToken)
    {
        if (!TryBuildChange(summary, out var change))
        {
            return document;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(change));
    }

    /// <summary>Builds the change that replaces the leading "Gets or sets" phrase with "Gets" in the first XML text token.</summary>
    /// <param name="summary">The summary element.</param>
    /// <param name="change">The resulting text change when the phrase is present.</param>
    /// <returns><see langword="true"/> when a change was produced.</returns>
    private static bool TryBuildChange(XmlElementSyntax summary, out TextChange change)
    {
        foreach (var token in summary.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
            {
                continue;
            }

            var value = token.ValueText.AsSpan();
            var start = 0;
            while (start < value.Length && char.IsWhiteSpace(value[start]))
            {
                start++;
            }

            if (!value[start..].StartsWith(ExistingPrefix.AsSpan(), StringComparison.Ordinal))
            {
                change = default;
                return false;
            }

            change = new TextChange(new(token.SpanStart + start, ExistingPrefix.Length), "Gets");
            return true;
        }

        change = default;
        return false;
    }
}
