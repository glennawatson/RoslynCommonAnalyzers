// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Sorts a documentation comment's elements into the conventional order (SST1666). Only the ranked elements
/// move, and they move between the positions they already occupy, so the <c>///</c> line structure and every
/// unranked element stay exactly where they were written.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1666DocumentationElementOrderCodeFixProvider))]
[Shared]
public sealed class Sst1666DocumentationElementOrderCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DocumentationRules.DocumentationElementOrder.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Order the documentation elements",
            nameof(Sst1666DocumentationElementOrderCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported comment and replaces it with one whose elements are in order.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        // A documentation comment is structured trivia, so the search has to be told to descend into it.
        if (root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true)?
                .FirstAncestorOrSelf<DocumentationCommentTriviaSyntax>() is not { } documentation)
        {
            return null;
        }

        return new NodeReplacement(documentation, Reorder(documentation));
    }

    /// <summary>Rebuilds a documentation comment with its ranked elements in the conventional order.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <returns>The reordered comment.</returns>
    /// <remarks>
    /// The ranked elements are permuted among the slots they already sit in, which leaves the interleaved
    /// <c>///</c> text nodes untouched. Elements of equal rank keep their relative order, so a set of
    /// <c>&lt;param&gt;</c> elements is left in whatever order the rule that matches them to the signature
    /// wants it in.
    /// </remarks>
    private static DocumentationCommentTriviaSyntax Reorder(DocumentationCommentTriviaSyntax documentation)
    {
        var content = documentation.Content;
        var isSlot = new bool[content.Count];
        var ranked = new List<(int Rank, int Position, XmlNodeSyntax Node)>();

        for (var i = 0; i < content.Count; i++)
        {
            var node = content[i];
            if (DocumentationElementOrder.NameOf(node) is not { } name)
            {
                continue;
            }

            var rank = DocumentationElementOrder.RankOf(name);
            if (rank < 0)
            {
                continue;
            }

            isSlot[i] = true;
            ranked.Add((rank, i, node));
        }

        ranked.Sort(static (left, right) => left.Rank == right.Rank
            ? left.Position.CompareTo(right.Position)
            : left.Rank.CompareTo(right.Rank));

        var rebuilt = new List<XmlNodeSyntax>(content.Count);
        var next = 0;
        for (var i = 0; i < content.Count; i++)
        {
            rebuilt.Add(isSlot[i] ? ranked[next++].Node : content[i]);
        }

        return documentation.WithContent(SyntaxFactory.List(rebuilt));
    }
}
