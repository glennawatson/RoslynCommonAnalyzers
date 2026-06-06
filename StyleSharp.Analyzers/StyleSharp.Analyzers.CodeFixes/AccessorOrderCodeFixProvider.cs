// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Reorders property/event accessors so get/add appears before set/remove (SST1212/SST1213).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AccessorOrderCodeFixProvider))]
[Shared]
public sealed class AccessorOrderCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        OrderingRules.PropertyAccessorOrder.Id,
        OrderingRules.EventAccessorOrder.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<AccessorListSyntax>() is not { } list)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Reorder accessors",
                    cancellationToken => ReorderAsync(context.Document, list, cancellationToken),
                    equivalenceKey: nameof(AccessorOrderCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Returns the canonical rank of an accessor — get/add before set/init/remove.</summary>
    /// <param name="accessor">The accessor.</param>
    /// <returns>0 for the primary accessor, 1 otherwise.</returns>
    private static int Rank(AccessorDeclarationSyntax accessor)
        => accessor.Keyword.IsKind(SyntaxKind.GetKeyword) || accessor.Keyword.IsKind(SyntaxKind.AddKeyword) ? 0 : 1;

    /// <summary>Reorders the accessor list into canonical order, keeping each slot's trivia.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="list">The accessor list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ReorderAsync(Document document, AccessorListSyntax list, CancellationToken cancellationToken)
    {
        var original = list.Accessors;
        var ordered = original.OrderBy(Rank).ToList();

        var rebuilt = new List<AccessorDeclarationSyntax>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            rebuilt.Add(ordered[index]
                .WithLeadingTrivia(original[index].GetLeadingTrivia())
                .WithTrailingTrivia(original[index].GetTrailingTrivia()));
        }

        var newList = list.WithAccessors(SyntaxFactory.List(rebuilt));
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(list, newList));
    }
}
