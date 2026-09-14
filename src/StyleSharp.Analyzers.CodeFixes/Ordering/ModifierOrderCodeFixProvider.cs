// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Reorders declaration modifiers into the canonical order (SST1206/SST1207).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModifierOrderCodeFixProvider))]
[Shared]
public sealed class ModifierOrderCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(
        FindReorderableNode,
        static (current, _) => Reorder(current, ModifierOrdering.Modifiers(current)));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        OrderingRules.DeclarationKeywordOrder.Id,
        OrderingRules.ProtectedBeforeInternal.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Reorder modifiers",
            nameof(ModifierOrderCodeFixProvider),
            FindReorderableNode,
            ReorderAsync);

    /// <summary>Reorders the node's modifiers canonically, keeping each slot's trivia.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="node">The declaration whose modifiers are reordered.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ReorderAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(Reorder(root!, ModifierOrdering.Modifiers(node)));
    }

    /// <summary>Resolves a diagnostic to the node that holds the reported modifiers, when there are at least two to reorder.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node holding the modifiers, or <see langword="null"/> when there is nothing to reorder.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxNode? FindReorderableNode(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is { } node
            && ModifierOrdering.Modifiers(node).Count >= 2
            ? node
            : null;

    /// <summary>Puts a modifier list into canonical order within the node that contains it.</summary>
    /// <typeparam name="TNode">The type of the node holding the modifiers.</typeparam>
    /// <param name="target">The node the modifiers belong to, or one of its ancestors.</param>
    /// <param name="modifiers">The modifiers to reorder.</param>
    /// <returns>The node with the modifiers sorted; each slot keeps the trivia it had.</returns>
    private static TNode Reorder<TNode>(TNode target, in SyntaxTokenList modifiers)
        where TNode : SyntaxNode
    {
        var sorted = new SyntaxToken[modifiers.Count];
        for (var i = 0; i < modifiers.Count; i++)
        {
            sorted[i] = modifiers[i];
        }

        Array.Sort(sorted, CompareModifiers);

        var replacements = new Dictionary<int, SyntaxToken>(modifiers.Count);
        for (var index = 0; index < modifiers.Count; index++)
        {
            var token = sorted[index];
            replacements[modifiers[index].SpanStart] = token.CopyAnnotationsTo(SyntaxFactory.Token(
                modifiers[index].LeadingTrivia,
                token.Kind(),
                token.Text,
                token.ValueText,
                modifiers[index].TrailingTrivia));
        }

        return target.ReplaceTokens(modifiers, (original, _) => replacements[original.SpanStart]);
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
