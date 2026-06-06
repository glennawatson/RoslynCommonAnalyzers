// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Helper methods for reformatting parameter and argument lists so each entry is on its own line.</summary>
internal static class UniqueLineCodeFixerHelper
{
    /// <summary>Returns the end-of-line trivia matching the line-ending convention of the supplied node's source text.</summary>
    /// <param name="node">The node whose source text to inspect.</param>
    /// <param name="elastic">
    /// If true, returns elastic trivia (suitable for the formatter to normalize); if false, returns
    /// non-elastic trivia (suitable for syntax forms the formatter would otherwise collapse, e.g. generic
    /// angle-bracket lists).
    /// </param>
    /// <returns>An end-of-line <see cref="SyntaxTrivia"/> using <c>\r\n</c> when the source contains a CRLF break, else <c>\n</c>.</returns>
    public static SyntaxTrivia GetEndOfLine(SyntaxNode node, bool elastic)
    {
        var text = node.SyntaxTree?.GetText().ToString() ?? string.Empty;
        var idx = text.IndexOf('\n');
        var crlf = idx > 0 && text[idx - 1] == '\r';
        var s = crlf ? "\r\n" : "\n";
        return elastic ? SyntaxFactory.ElasticEndOfLine(s) : SyntaxFactory.EndOfLine(s);
    }

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
        var entries = list.Value;
        if (entries.Count <= 1 || AllOnLine(entries, startLine))
        {
            return null;
        }

        var endOfLine = GetEndOfLine(node, elastic: true);

        // Indent each entry one level deeper than the owning declaration/expression.
        var leadingSpaces = GetLeadingSpaces(node) + 4;
        var indentedEntries = IndentEntries(entries, leadingSpaces);
        var separators = CreateSeparators(indentedEntries.Length, endOfLine);

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
        if (list.Count <= 1 || AllOnLine(list, startLine))
        {
            return null;
        }

        var endOfLine = GetEndOfLine(ownerNode, elastic: false);

        var leadingSpaces = GetLeadingSpaces(ownerNode) + 4;
        var indentedEntries = IndentEntries(list, leadingSpaces);
        var separators = CreateSeparators(indentedEntries.Length, endOfLine);

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

        var count = 0;
        while (count < lineText.Length && char.IsWhiteSpace(lineText[count]))
        {
            count++;
        }

        return count;
    }

    /// <summary>Returns whether every entry in a separated list begins on <paramref name="startLine"/>.</summary>
    /// <typeparam name="TParam">The list entry type.</typeparam>
    /// <param name="list">The separated list to inspect.</param>
    /// <param name="startLine">The line to compare against.</param>
    /// <returns><see langword="true"/> when every entry starts on the same line.</returns>
    private static bool AllOnLine<TParam>(SeparatedSyntaxList<TParam> list, int startLine)
        where TParam : SyntaxNode
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].GetLocation().GetLineSpan().StartLinePosition.Line != startLine)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Clones list entries with normalized indentation.</summary>
    /// <typeparam name="TParam">The list entry type.</typeparam>
    /// <param name="list">The separated list to indent.</param>
    /// <param name="leadingSpaces">The number of leading spaces to apply.</param>
    /// <returns>An array containing the indented entries.</returns>
    private static TParam[] IndentEntries<TParam>(SeparatedSyntaxList<TParam> list, int leadingSpaces)
        where TParam : SyntaxNode
    {
        var indentedEntries = new TParam[list.Count];
        var trivia = SyntaxFactory.Whitespace(new string(' ', leadingSpaces));
        for (var i = 0; i < list.Count; i++)
        {
            indentedEntries[i] = list[i].WithLeadingTrivia(trivia);
        }

        return indentedEntries;
    }

    /// <summary>Creates comma separators that preserve the owner's line-ending convention.</summary>
    /// <param name="entryCount">The number of entries in the separated list.</param>
    /// <param name="endOfLine">The end-of-line trivia to apply after each comma.</param>
    /// <returns>An array of separators sized for the entry count.</returns>
    private static SyntaxToken[] CreateSeparators(int entryCount, SyntaxTrivia endOfLine)
    {
        if (entryCount <= 1)
        {
            return [];
        }

        var separators = new SyntaxToken[entryCount - 1];
        for (var i = 0; i < separators.Length; i++)
        {
            separators[i] = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(endOfLine);
        }

        return separators;
    }
}
