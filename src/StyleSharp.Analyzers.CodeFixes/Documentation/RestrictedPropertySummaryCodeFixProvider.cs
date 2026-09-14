// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Replaces "Gets or sets" with "Gets" for SST1624.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RestrictedPropertySummaryCodeFixProvider))]
[Shared]
public sealed class RestrictedPropertySummaryCodeFixProvider : CodeFixProvider
{
    /// <summary>The phrase removed by the fix.</summary>
    private const string ExistingPrefix = "Gets or sets";

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.PropertySummaryOmitsRestrictedSetter.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Describe the readable accessor only",
            nameof(RestrictedPropertySummaryCodeFixProvider),
            DocumentationElementFix.FindElement,
            ApplyAsync);

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
    internal static Task<Document> ApplyAsync(Document document, XmlElementSyntax summary, CancellationToken cancellationToken) =>
        TryBuildChange(summary, out var change)
            ? DocumentationElementFix.ApplyAsync(document, change, cancellationToken)
            : Task.FromResult(document);

    /// <summary>Builds the change that replaces the leading "Gets or sets" phrase with "Gets" in the first XML text token.</summary>
    /// <param name="summary">The summary element.</param>
    /// <param name="change">The resulting text change when the phrase is present.</param>
    /// <returns><see langword="true"/> when a change was produced.</returns>
    private static bool TryBuildChange(XmlElementSyntax summary, out TextChange change)
    {
        var token = default(SyntaxToken);
        _ = DescendantTraversalHelper.VisitDescendantTokens(
            summary,
            ref token,
            static (in SyntaxToken current, ref SyntaxToken firstText) =>
            {
                if (!current.IsKind(SyntaxKind.XmlTextLiteralToken))
                {
                    return true;
                }

                firstText = current;
                return false;
            });

        if (token.RawKind == 0)
        {
            change = default;
            return false;
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

        change = new(new(token.SpanStart + start, ExistingPrefix.Length), "Gets");
        return true;
    }
}
