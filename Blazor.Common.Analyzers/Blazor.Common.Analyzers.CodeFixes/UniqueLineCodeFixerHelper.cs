// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Blazor.Common.Analyzers;

/// <summary>Helper methods for reformatting parameter and argument lists so each entry is on its own line.</summary>
internal static class UniqueLineCodeFixerHelper
{
    /// <summary>Rewrites the node with each list entry placed on its own indented line, or returns <see langword="null"/> if no change is needed.</summary>
    /// <typeparam name="T">The type of syntax node owning the list.</typeparam>
    /// <typeparam name="TParam">The type of the list entries.</typeparam>
    /// <param name="node">The node whose list should be reformatted.</param>
    /// <param name="converterToList">A function that extracts the separated list from the node.</param>
    /// <param name="addParameters">A function that produces a new node with the supplied separated list.</param>
    /// <returns>The rewritten node, or <see langword="null"/> if the list is absent or already spans a single line.</returns>
    public static T? ConvertNodeIfAble<T, TParam>(
        this T node,
        Func<T, SeparatedSyntaxList<TParam>?> converterToList,
        Func<T, SeparatedSyntaxList<TParam>, T> addParameters)
        where T : SyntaxNode
        where TParam : SyntaxNode
    {
        var list = converterToList(node);

        if (list is null)
        {
            return null;
        }

        // Bail out unless the entries actually span more than one line.
        var startLine = node.GetLocation().GetLineSpan().StartLinePosition.Line;
        if (list.Value.Count <= 1 || list.Value.All(p => p.GetLocation().GetLineSpan().StartLinePosition.Line == startLine))
        {
            return null;
        }

        // Indent each entry one level deeper than the owning declaration/expression.
        var leadingSpaces = GetLeadingSpaces(node) + 4;
        var indentedEntries = list.Value
            .Select(a => a.WithLeadingTrivia(SyntaxFactory.Whitespace(new string(' ', leadingSpaces))))
            .ToList();

        var separators = Enumerable.Repeat(
            SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.ElasticLineFeed),
            indentedEntries.Count - 1);

        return addParameters(node, SyntaxFactory.SeparatedList(indentedEntries, separators));
    }

    /// <summary>Rewrites a separated list so each entry sits on its own indented line, or returns <see langword="null"/> if no change is needed.</summary>
    /// <typeparam name="TParam">The type of the list entries.</typeparam>
    /// <param name="ownerNode">The list node (such as a type parameter or type argument list) owning the entries.</param>
    /// <param name="list">The separated list of entries to reformat.</param>
    /// <returns>The reformatted separated list, or <see langword="null"/> if the list has a single entry or already spans a single line.</returns>
    public static SeparatedSyntaxList<TParam>? SplitEntriesOntoOwnLines<TParam>(SyntaxNode ownerNode, SeparatedSyntaxList<TParam> list)
        where TParam : SyntaxNode
    {
        var startLine = ownerNode.GetLocation().GetLineSpan().StartLinePosition.Line;
        if (list.Count <= 1 || list.All(p => p.GetLocation().GetLineSpan().StartLinePosition.Line == startLine))
        {
            return null;
        }

        var leadingSpaces = GetLeadingSpaces(ownerNode) + 4;
        var indentedEntries = list
            .Select(a => a.WithLeadingTrivia(SyntaxFactory.Whitespace(new string(' ', leadingSpaces))))
            .ToList();

        var separators = Enumerable.Repeat(
            SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.LineFeed),
            indentedEntries.Count - 1);

        return SyntaxFactory.SeparatedList(indentedEntries, separators);
    }

    /// <summary>Gets the number of leading whitespace characters on the line where the node begins.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>The count of leading whitespace characters, or zero if it cannot be determined.</returns>
    private static int GetLeadingSpaces(SyntaxNode? node)
    {
        if (node?.SyntaxTree is not { } tree)
        {
            return 0;
        }

        var lineSpan = node.GetLocation().GetLineSpan();
        var lineText = tree.GetText().Lines[lineSpan.StartLinePosition.Line].ToString();

        return lineText.TakeWhile(char.IsWhiteSpace).Count();
    }
}
