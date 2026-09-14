// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Prefixes a property summary with the accessor phrase ("Gets ", "Sets ", "Gets or sets ") (SST1623).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PropertySummaryCodeFixProvider))]
[Shared]
public sealed class PropertySummaryCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.PropertySummaryAccessors.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            nameof(PropertySummaryCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (DocumentationElementFix.FindElement(root, diagnostic) is not { } summary
            || XmlDocumentationHelper.DocumentedMember(summary) is not PropertyDeclarationSyntax property)
        {
            return;
        }

        var prefix = DocumentationConventions.PropertyAccessorPrefix(property);
        if (!TryBuildChange(summary, prefix, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Builds the prefix-insertion change that lower-cases the first existing word of the summary.</summary>
    /// <param name="summary">The summary element.</param>
    /// <param name="prefix">The accessor prefix.</param>
    /// <param name="change">The resulting text change when the summary has a first text character.</param>
    /// <returns><see langword="true"/> when a change was produced.</returns>
    private static bool TryBuildChange(XmlElementSyntax summary, string prefix, out TextChange change)
    {
        if (!XmlDocumentationHelper.TryGetFirstTextCharacter(summary, out var first, out var position))
        {
            change = default;
            return false;
        }

        change = new(new(position, 1), prefix + char.ToLowerInvariant(first));
        return true;
    }

    /// <summary>Words the action for the reported property summary with the accessor phrase it gains.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the element no longer documents a property.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        DocumentationElementFix.FindElement(root, diagnostic) is { } summary && XmlDocumentationHelper.DocumentedMember(summary) is PropertyDeclarationSyntax property
            ? TitleFor(DocumentationConventions.PropertyAccessorPrefix(property))
            : null;

    /// <summary>Words the action for an accessor prefix.</summary>
    /// <param name="prefix">The accessor phrase the summary gains.</param>
    /// <returns>The code action title.</returns>
    private static string TitleFor(string prefix) => prefix switch
    {
        "Gets or sets " => "Prefix summary with 'Gets or sets'",
        "Sets " => "Prefix summary with 'Sets'",
        _ => "Prefix summary with 'Gets'",
    };
}
