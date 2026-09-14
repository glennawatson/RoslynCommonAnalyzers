// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
public sealed class Sst1661CodeTagContentCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(DocumentationRules.CodeTagContent.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => TryGetSwap(root, diagnostic, out _, out var target) ? $"Use the '<{target}>' tag" : null,
            nameof(Sst1661CodeTagContentCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryGetSwap(root, diagnostic, out var element, out var target))
        {
            return;
        }

        changes.Add(new(element.StartTag.Name.Span, target));
        changes.Add(new(element.EndTag.Name.Span, target));
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

        if (DocumentationElementFix.FindElement(root, diagnostic) is not { } found
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
