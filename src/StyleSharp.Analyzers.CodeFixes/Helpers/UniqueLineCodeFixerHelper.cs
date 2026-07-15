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
        var text = node.SyntaxTree?.GetText();
        if (text is null)
        {
            return elastic ? SyntaxFactory.ElasticEndOfLine("\n") : SyntaxFactory.EndOfLine("\n");
        }

        var s = "\n";
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            s = i > 0 && text[i - 1] == '\r' ? "\r\n" : "\n";
            break;
        }

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

        var entries = list.Value;
        if (entries.Count <= 1 || node.SyntaxTree is not { } tree)
        {
            return null;
        }

        // Start positions are monotonic, so the last entry tells us whether the list stayed on one line.
        var startLine = tree.GetLineSpan(node.Span).StartLinePosition.Line;
        if (tree.GetLineSpan(entries[entries.Count - 1].Span).StartLinePosition.Line == startLine)
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
        if (list.Count <= 1 || ownerNode.SyntaxTree is not { } tree)
        {
            return null;
        }

        var startLine = tree.GetLineSpan(ownerNode.Span).StartLinePosition.Line;
        if (tree.GetLineSpan(list[list.Count - 1].Span).StartLinePosition.Line == startLine)
        {
            return null;
        }

        var endOfLine = GetEndOfLine(ownerNode, elastic: false);

        var leadingSpaces = GetLeadingSpaces(ownerNode) + 4;
        var indentedEntries = IndentEntries(list, leadingSpaces);
        var separators = CreateSeparators(indentedEntries.Length, endOfLine);

        return SyntaxFactory.SeparatedList(indentedEntries, separators);
    }

    /// <summary>Rewrites a node's parenthesized parameter list so each parameter sits on its own line.</summary>
    /// <typeparam name="T">The type of syntax node owning the parameter list.</typeparam>
    /// <param name="node">The node whose parameter list should be reformatted.</param>
    /// <param name="getParameterList">Reads the parenthesized parameter list from the node.</param>
    /// <param name="withParameterList">Produces a copy of the node carrying the supplied parameter list.</param>
    /// <returns>The rewritten node, or the original when it has no parameter list or already spans one line per parameter.</returns>
    public static T SplitParametersOntoOwnLines<T>(
        T node,
        Func<T, ParameterListSyntax?> getParameterList,
        Func<T, ParameterListSyntax, T> withParameterList)
        where T : SyntaxNode
    {
        var endOfLine = GetEndOfLine(node, elastic: true);
        return node.ConvertNodeIfAble(
                   inner => getParameterList(inner)?.Parameters,
                   (inner, parameters) => withParameterList(
                       inner,
                       SyntaxFactory.ParameterList(parameters)
                           .WithOpenParenToken(getParameterList(inner)!.OpenParenToken.WithTrailingTrivia(endOfLine))))
               ?? node;
    }

    /// <summary>Rewrites a node's parenthesized argument list so each argument sits on its own line.</summary>
    /// <typeparam name="T">The type of syntax node owning the argument list.</typeparam>
    /// <param name="node">The node whose argument list should be reformatted.</param>
    /// <param name="getArgumentList">Reads the parenthesized argument list from the node.</param>
    /// <param name="withArgumentList">Produces a copy of the node carrying the supplied argument list.</param>
    /// <returns>The rewritten node, or the original when it has no argument list or already spans one line per argument.</returns>
    public static T SplitArgumentsOntoOwnLines<T>(
        T node,
        Func<T, ArgumentListSyntax?> getArgumentList,
        Func<T, ArgumentListSyntax, T> withArgumentList)
        where T : SyntaxNode
    {
        var endOfLine = GetEndOfLine(node, elastic: true);
        return node.ConvertNodeIfAble(
                   inner => getArgumentList(inner)?.Arguments,
                   (inner, arguments) => withArgumentList(
                       inner,
                       SyntaxFactory.ArgumentList(arguments)
                           .WithOpenParenToken(getArgumentList(inner)!.OpenParenToken.WithTrailingTrivia(endOfLine))))
               ?? node;
    }

    /// <summary>Rewrites an angle-bracketed list node (type parameter, type argument, or function-pointer list) so each entry sits on its own line.</summary>
    /// <typeparam name="T">The angle-bracketed list node type.</typeparam>
    /// <typeparam name="TParam">The type of the list entries.</typeparam>
    /// <param name="node">The angle-bracketed list node to reformat.</param>
    /// <param name="entries">The separated list of entries the node owns.</param>
    /// <param name="rebuild">Builds the reformatted node from the split entries and the owner's end-of-line trivia.</param>
    /// <returns>The rewritten node, or the original when it has a single entry or already spans a single line.</returns>
    public static T SplitAngleBracketedListOntoOwnLines<T, TParam>(
        T node,
        SeparatedSyntaxList<TParam> entries,
        Func<SeparatedSyntaxList<TParam>, SyntaxTrivia, T> rebuild)
        where T : SyntaxNode
        where TParam : SyntaxNode
    {
        var newList = SplitEntriesOntoOwnLines(node, entries);
        if (newList is null)
        {
            return node;
        }

        var endOfLine = GetEndOfLine(node, elastic: false);
        return rebuild(newList.Value, endOfLine);
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

        var text = tree.GetText();
        var lineNumber = tree.GetLineSpan(node.Span).StartLinePosition.Line;
        var line = text.Lines[lineNumber];

        var count = 0;
        for (var position = line.Start; position < line.End; position++)
        {
            if (!char.IsWhiteSpace(text[position]))
            {
                break;
            }

            count++;
        }

        return count;
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
        var trivia = SyntaxFactory.Whitespace(new(' ', leadingSpaces));
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
