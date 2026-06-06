// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
        var ordered = new AccessorDeclarationSyntax[original.Count];
        for (var i = 0; i < original.Count; i++)
        {
            ordered[i] = original[i];
        }

        Array.Sort(ordered, CompareAccessors);

        var rebuilt = new AccessorDeclarationSyntax[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            rebuilt[index] = ordered[index]
                .WithLeadingTrivia(original[index].GetLeadingTrivia())
                .WithTrailingTrivia(original[index].GetTrailingTrivia());
        }

        var newList = list.WithAccessors(SyntaxFactory.List(rebuilt));
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(list, newList));
    }

    /// <summary>Compares two accessors by the canonical accessor order.</summary>
    /// <param name="left">The left accessor.</param>
    /// <param name="right">The right accessor.</param>
    /// <returns>A negative value when <paramref name="left"/> sorts first, positive when last, zero when equal.</returns>
    private static int CompareAccessors(AccessorDeclarationSyntax left, AccessorDeclarationSyntax right)
        => Rank(left) - Rank(right);
}
