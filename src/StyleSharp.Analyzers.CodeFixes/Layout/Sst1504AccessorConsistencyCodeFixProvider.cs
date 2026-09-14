// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Makes a property's accessors consistent (SST1504) by expanding every single-line
/// block accessor onto multiple lines, matching the multi-line accessor(s). The rewrite
/// is skipped when a comment shares a line with an accessor body.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1504AccessorConsistencyCodeFixProvider))]
[Shared]
public sealed class Sst1504AccessorConsistencyCodeFixProvider : CodeFixProvider
{
    /// <summary>The fixed brace/open-close edits added when expanding one single-line block accessor.</summary>
    private const int BlockExpansionBaseChanges = 2;

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.AccessorLineConsistency.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Expand accessors onto multiple lines",
            nameof(Sst1504AccessorConsistencyCodeFixProvider),
            static (root, diagnostic) => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent as AccessorListSyntax,
            ExpandAsync);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not AccessorListSyntax list)
        {
            return;
        }

        var local = new List<TextChange>(EstimatedChangeCapacity(list));
        if (!TryBuildChanges(text, list, local))
        {
            return;
        }

        changes.AddRange(local);
    }

    /// <summary>Expands every single-line block accessor in the list onto multiple lines.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="list">The accessor list to normalise.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document, unchanged when a comment blocks the rewrite.</returns>
    internal static async Task<Document> ExpandAsync(Document document, AccessorListSyntax list, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<TextChange>(EstimatedChangeCapacity(list));

        return !TryBuildChanges(text, list, changes) || changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Appends the changes that expand every single-line block accessor in the list.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="list">The accessor list to normalise.</param>
    /// <param name="changes">The change set to append to.</param>
    /// <returns><see langword="true"/> when the rewrite is safe; <see langword="false"/> when a comment blocks it.</returns>
    private static bool TryBuildChanges(SourceText text, AccessorListSyntax list, List<TextChange> changes)
    {
        var newLine = LayoutFixHelpers.DetectNewLine(text);

        foreach (var accessor in list.Accessors)
        {
            if (accessor.Body is not { } body
                || LayoutHelpers.StartLine(text, body.OpenBraceToken) != LayoutHelpers.StartLine(text, body.CloseBraceToken))
            {
                continue;
            }

            if (!LayoutFixHelpers.TryAppendBlockExpansion(text, body, newLine, changes))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Estimates the number of text changes needed to expand single-line block accessors.</summary>
    /// <param name="list">The accessor list to inspect.</param>
    /// <returns>A conservative initial capacity for the change list.</returns>
    private static int EstimatedChangeCapacity(AccessorListSyntax list)
    {
        var capacity = 0;
        for (var i = 0; i < list.Accessors.Count; i++)
        {
            if (list.Accessors[i].Body is { } body)
            {
                capacity += body.Statements.Count + BlockExpansionBaseChanges;
            }
        }

        return capacity;
    }
}
