// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Reorders declaration modifiers into the canonical order (SST1206/SST1207).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModifierOrderCodeFixProvider))]
[Shared]
public sealed class ModifierOrderCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        OrderingRules.DeclarationKeywordOrder.Id,
        OrderingRules.ProtectedBeforeInternal.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not { } node || ModifierOrdering.Modifiers(node).Count < 2)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Reorder modifiers",
                    cancellationToken => ReorderAsync(context.Document, node, cancellationToken),
                    equivalenceKey: nameof(ModifierOrderCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Reorders the node's modifiers canonically, keeping each slot's trivia.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="node">The declaration whose modifiers are reordered.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ReorderAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
    {
        var modifiers = ModifierOrdering.Modifiers(node);
        var sorted = new SyntaxToken[modifiers.Count];
        for (var i = 0; i < modifiers.Count; i++)
        {
            sorted[i] = modifiers[i];
        }

        Array.Sort(sorted, CompareModifiers);

        var replacements = new Dictionary<int, SyntaxToken>(modifiers.Count);
        for (var index = 0; index < modifiers.Count; index++)
        {
            replacements[modifiers[index].SpanStart] = sorted[index]
                .WithLeadingTrivia(modifiers[index].LeadingTrivia)
                .WithTrailingTrivia(modifiers[index].TrailingTrivia);
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceTokens(modifiers, (original, _) => replacements[original.SpanStart]);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>Compares two modifiers by declaration rank, then access rank for ties.</summary>
    /// <param name="left">The left modifier token.</param>
    /// <param name="right">The right modifier token.</param>
    /// <returns>A negative value when <paramref name="left"/> sorts first, positive when last, zero when equal.</returns>
    private static int CompareModifiers(SyntaxToken left, SyntaxToken right)
    {
        var rankDifference = ModifierOrdering.Rank(left) - ModifierOrdering.Rank(right);
        return rankDifference != 0 ? rankDifference : ModifierOrdering.AccessRank(left) - ModifierOrdering.AccessRank(right);
    }
}
