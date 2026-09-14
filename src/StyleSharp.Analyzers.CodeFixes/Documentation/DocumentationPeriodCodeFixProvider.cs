// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Appends a terminal period to documentation prose that is missing one (SST1629).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DocumentationPeriodCodeFixProvider))]
[Shared]
public sealed class DocumentationPeriodCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.TextMustEndWithPeriod.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync<int>(
            context,
            "Add a period",
            nameof(DocumentationPeriodCodeFixProvider),
            TryFindInsertPosition,
            AddPeriodAsync);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryFindInsertPosition(root, diagnostic, out var insertPosition))
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Task<Document> AddPeriodAsync(Document document, int position, CancellationToken cancellationToken) =>
        DocumentationElementFix.ApplyAsync(document, BuildChange(position), cancellationToken);

    /// <summary>Builds the change that inserts a period at <paramref name="position"/>.</summary>
    /// <param name="position">The source position to insert the period at.</param>
    /// <returns>The period-insertion text change.</returns>
    private static TextChange BuildChange(int position) => new(new(position, 0), ".");

    /// <summary>Finds where the reported documentation element needs its terminal period.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="position">The source position to insert the period at.</param>
    /// <returns><see langword="true"/> when the reported element still lacks a terminal period.</returns>
    private static bool TryFindInsertPosition(SyntaxNode root, Diagnostic diagnostic, out int position)
    {
        position = 0;
        return DocumentationElementFix.FindElement(root, diagnostic) is { } element
            && XmlDocumentationHelper.NeedsTerminalPeriod(element, out position);
    }
}
