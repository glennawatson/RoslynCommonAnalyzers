// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Makes a property's accessors consistent (SST1504) by expanding every single-line
/// block accessor onto multiple lines, matching the multi-line accessor(s). The rewrite
/// is skipped when a comment shares a line with an accessor body.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AccessorConsistencyCodeFixProvider))]
[Shared]
public sealed class AccessorConsistencyCodeFixProvider : CodeFixProvider
{
    /// <summary>The fixed brace/open-close edits added when expanding one single-line block accessor.</summary>
    private const int BlockExpansionBaseChanges = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.AccessorLineConsistency.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not AccessorListSyntax list)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Expand accessors onto multiple lines",
                    cancellationToken => ExpandAsync(context.Document, list, cancellationToken),
                    equivalenceKey: nameof(AccessorConsistencyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Expands every single-line block accessor in the list onto multiple lines.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="list">The accessor list to normalise.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document, unchanged when a comment blocks the rewrite.</returns>
    internal static async Task<Document> ExpandAsync(Document document, AccessorListSyntax list, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var changes = new List<TextChange>(EstimatedChangeCapacity(list));

        foreach (var accessor in list.Accessors)
        {
            if (accessor.Body is not { } body
                || LayoutHelpers.StartLine(text, body.OpenBraceToken) != LayoutHelpers.StartLine(text, body.CloseBraceToken))
            {
                continue;
            }

            if (!LayoutFixHelpers.TryAppendBlockExpansion(text, body, newLine, changes))
            {
                return document;
            }
        }

        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
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
