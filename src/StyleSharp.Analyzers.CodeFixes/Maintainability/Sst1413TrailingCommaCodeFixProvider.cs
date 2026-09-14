// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Adds a trailing comma after the last element of a multi-line initializer (SST1413).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1413TrailingCommaCodeFixProvider))]
[Shared]
public sealed class Sst1413TrailingCommaCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.TrailingComma.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Add trailing comma",
            nameof(Sst1413TrailingCommaCodeFixProvider),
            static (root, diagnostic) => root.FindNode(diagnostic.Location.SourceSpan),
            static (document, element, cancellationToken) => AddCommaAsync(document, element.Span.End, cancellationToken));

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var element = root.FindNode(diagnostic.Location.SourceSpan);
        changes.Add(BuildChange(element.Span.End));
    }

    /// <summary>Inserts a comma at the given position.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="position">The position after the last element.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> AddCommaAsync(Document document, int position, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(BuildChange(position)));
    }

    /// <summary>Builds the comma-insertion change at the given position.</summary>
    /// <param name="position">The position after the last element.</param>
    /// <returns>The text change that inserts the trailing comma.</returns>
    private static TextChange BuildChange(int position) => new(new(position, 0), ",");
}
