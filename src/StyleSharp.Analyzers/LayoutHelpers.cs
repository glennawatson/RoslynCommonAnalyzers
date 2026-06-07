// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared, allocation-free helpers for the layout (SST15xx) rules. Every member is
/// <see langword="static"/> and operates on the passed-in <see cref="SourceText"/> or
/// syntax — no per-call state, mirroring the library's helper-over-inheritance design.
/// Line numbers come from the tree's cached line table (a binary search, no heap
/// allocation) and blankness is decided by scanning characters rather than materialising
/// strings, so the no-diagnostic path stays free of allocations.
/// </summary>
internal static class LayoutHelpers
{
    /// <summary>Returns the zero-based line index that contains <paramref name="position"/>.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">An absolute character position in the text.</param>
    /// <returns>The zero-based line index.</returns>
    public static int LineOf(SourceText text, int position) => text.Lines.GetLineFromPosition(position).LineNumber;

    /// <summary>Returns the zero-based line index on which <paramref name="token"/> starts.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="token">The token.</param>
    /// <returns>The zero-based start line index.</returns>
    public static int StartLine(SourceText text, SyntaxToken token) => LineOf(text, token.SpanStart);

    /// <summary>Returns the zero-based line index on which <paramref name="token"/> ends.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="token">The token.</param>
    /// <returns>The zero-based end line index.</returns>
    public static int EndLine(SourceText text, SyntaxToken token)
        => LineOf(text, token.Span.End > token.SpanStart ? token.Span.End - 1 : token.SpanStart);

    /// <summary>Returns whether the line at <paramref name="lineIndex"/> is empty or all whitespace.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="lineIndex">The zero-based line index.</param>
    /// <returns><see langword="true"/> when the line has no non-whitespace content.</returns>
    public static bool IsBlankLine(SourceText text, int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= text.Lines.Count)
        {
            return false;
        }

        var line = text.Lines[lineIndex];
        for (var position = line.Start; position < line.End; position++)
        {
            if (!char.IsWhiteSpace(text[position]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the line on which a member's content begins, counting a leading doc or comment header.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="member">The member node.</param>
    /// <returns>The zero-based line index of the member's first documentation/comment trivia or first token.</returns>
    public static int ContentStartLine(SourceText text, SyntaxNode member) =>
        TryGetHeaderStartLine(text, member, out var startLine)
            ? startLine
            : StartLine(text, member.GetFirstToken());

    /// <summary>Returns the starting line of leading comment or documentation trivia, when present.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="member">The member node.</param>
    /// <param name="startLine">The line of the first leading header trivia.</param>
    /// <returns><see langword="true"/> when the member has a leading comment or documentation header.</returns>
    public static bool TryGetHeaderStartLine(SourceText text, SyntaxNode member, out int startLine)
    {
        var leadingTrivia = member.GetLeadingTrivia();
        if (leadingTrivia.Count == 0)
        {
            startLine = 0;
            return false;
        }

        foreach (var trivia in leadingTrivia)
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                continue;
            }

            startLine = LineOf(text, trivia.SpanStart);
            return true;
        }

        startLine = 0;
        return false;
    }

    /// <summary>Returns the line on which a member's content begins, using the running line cursor when practical.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="member">The member node.</param>
    /// <param name="lineNumber">The current line number cursor.</param>
    /// <param name="line">The current line cursor.</param>
    /// <returns>The zero-based line index of the member's first header trivia or first token.</returns>
    public static int ContentStartLineOrLater(SourceText text, SyntaxNode member, ref int lineNumber, ref TextLine line)
    {
        var leadingTrivia = member.GetLeadingTrivia();
        if (leadingTrivia.Count == 0)
        {
            return LineOfOrLater(text, member.GetFirstToken().SpanStart, ref lineNumber, ref line);
        }

        foreach (var trivia in leadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                return LineOfOrLater(text, trivia.SpanStart, ref lineNumber, ref line);
            }
        }

        return LineOfOrLater(text, member.GetFirstToken().SpanStart, ref lineNumber, ref line);
    }

    /// <summary>Returns the zero-based line number for a position on or after the current cursor.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The position to look up.</param>
    /// <param name="lineNumber">The current line number cursor.</param>
    /// <param name="line">The current line cursor.</param>
    /// <returns>The resolved line number.</returns>
    public static int LineOfOrLater(SourceText text, int position, ref int lineNumber, ref TextLine line)
    {
        while (position >= line.EndIncludingLineBreak && lineNumber + 1 < text.Lines.Count)
        {
            lineNumber++;
            line = text.Lines[lineNumber];
        }

        return lineNumber;
    }

    /// <summary>Returns the zero-based start and end line numbers for a span on or after the current cursor.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The span start to look up.</param>
    /// <param name="end">The span end to look up.</param>
    /// <param name="lineNumber">The current line number cursor.</param>
    /// <param name="line">The current line cursor.</param>
    /// <param name="startLine">The resolved start line.</param>
    /// <param name="endLine">The resolved end line.</param>
    public static void GetLineSpanOfOrLater(
        SourceText text,
        int start,
        int end,
        ref int lineNumber,
        ref TextLine line,
        out int startLine,
        out int endLine)
    {
        startLine = LineOfOrLater(text, start, ref lineNumber, ref line);
        if (end <= line.EndIncludingLineBreak)
        {
            endLine = lineNumber;
            return;
        }

        endLine = LineOfOrLater(text, end, ref lineNumber, ref line);
    }

    /// <summary>Returns whether a token is the first token on its line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="token">The token to inspect.</param>
    /// <param name="tokenLine">The token's line.</param>
    /// <returns><see langword="true"/> when no earlier token shares the line.</returns>
    public static bool TokenStartsLine(SourceText text, SyntaxToken token, int tokenLine)
    {
        var previous = token.GetPreviousToken();
        return previous.IsKind(SyntaxKind.None) || EndLine(text, previous) < tokenLine;
    }

    /// <summary>Returns whether a token shares its line with the token before it.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="token">The token to inspect.</param>
    /// <param name="tokenLine">The token's line.</param>
    /// <returns><see langword="true"/> when code precedes the token on its line.</returns>
    public static bool TokenSharesLineWithPrevious(SourceText text, SyntaxToken token, int tokenLine)
    {
        var previous = token.GetPreviousToken();
        return !previous.IsKind(SyntaxKind.None) && EndLine(text, previous) == tokenLine;
    }

    /// <summary>Returns whether a token shares its line with the token after it.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="token">The token to inspect.</param>
    /// <param name="tokenLine">The token's line.</param>
    /// <returns><see langword="true"/> when code follows the token on its line.</returns>
    public static bool TokenSharesLineWithNext(SourceText text, SyntaxToken token, int tokenLine)
    {
        var next = token.GetNextToken();
        return !next.IsKind(SyntaxKind.None) && StartLine(text, next) == tokenLine;
    }

    /// <summary>Returns the combined line-sharing facts for a token in one neighbor scan.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="token">The token to inspect.</param>
    /// <param name="tokenLine">The token's line.</param>
    /// <returns>The token's line-sharing facts.</returns>
    public static TokenLineFacts GetTokenLineFacts(SourceText text, SyntaxToken token, int tokenLine)
    {
        var previous = token.GetPreviousToken();
        var sharesLineWithPrevious = !previous.IsKind(SyntaxKind.None) && EndLine(text, previous) == tokenLine;
        var next = token.GetNextToken();
        var sharesLineWithNext = !next.IsKind(SyntaxKind.None) && StartLine(text, next) == tokenLine;
        return new(!sharesLineWithPrevious, sharesLineWithPrevious, sharesLineWithNext);
    }

    /// <summary>The control-flow statement kinds that carry a single embedded child statement.</summary>
    /// <returns>The handled control-flow kinds.</returns>
    public static ImmutableArray<SyntaxKind> EmbeddedStatementKinds() => ImmutableArrays.Of(
        SyntaxKind.IfStatement,
        SyntaxKind.ElseClause,
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.ForEachVariableStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.DoStatement,
        SyntaxKind.UsingStatement,
        SyntaxKind.LockStatement,
        SyntaxKind.FixedStatement);

    /// <summary>Extracts the embedded child statement of a control-flow node.</summary>
    /// <param name="node">The control-flow node.</param>
    /// <param name="statement">The embedded statement, when present.</param>
    /// <returns><see langword="true"/> when the node has an embedded statement.</returns>
    public static bool TryGetEmbeddedStatement(SyntaxNode node, out StatementSyntax statement)
    {
        statement = (EmbeddedStatementOrNull(node) ?? SecondaryEmbeddedStatementOrNull(node))!;
        return statement is not null;
    }

    /// <summary>Finds the leading XML documentation header of a member, when present.</summary>
    /// <param name="member">The member node.</param>
    /// <param name="header">The documentation trivia, when found.</param>
    /// <returns><see langword="true"/> when the member carries a documentation header.</returns>
    public static bool TryGetDocHeader(SyntaxNode member, out SyntaxTrivia header)
    {
        foreach (var trivia in member.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                continue;
            }

            header = trivia;
            return true;
        }

        header = default;
        return false;
    }

    /// <summary>Extracts the opening and closing braces of a brace-bearing node, when both are present.</summary>
    /// <param name="node">The candidate node.</param>
    /// <param name="open">The opening brace token, when found.</param>
    /// <param name="close">The closing brace token, when found.</param>
    /// <returns><see langword="true"/> when the node carries a real (non-missing) brace pair.</returns>
    public static bool TryGetBraces(SyntaxNode node, out SyntaxToken open, out SyntaxToken close)
    {
        switch (node)
        {
            case BlockSyntax block:
            {
                open = block.OpenBraceToken;
                close = block.CloseBraceToken;
                break;
            }

            case AccessorListSyntax accessors:
            {
                open = accessors.OpenBraceToken;
                close = accessors.CloseBraceToken;
                break;
            }

            case BaseTypeDeclarationSyntax type:
            {
                open = type.OpenBraceToken;
                close = type.CloseBraceToken;
                break;
            }

            case NamespaceDeclarationSyntax ns:
            {
                open = ns.OpenBraceToken;
                close = ns.CloseBraceToken;
                break;
            }

            case SwitchStatementSyntax @switch:
            {
                open = @switch.OpenBraceToken;
                close = @switch.CloseBraceToken;
                break;
            }

            case SwitchExpressionSyntax switchExpression:
            {
                open = switchExpression.OpenBraceToken;
                close = switchExpression.CloseBraceToken;
                break;
            }

            case InitializerExpressionSyntax initializer:
            {
                open = initializer.OpenBraceToken;
                close = initializer.CloseBraceToken;
                break;
            }

            case AnonymousObjectCreationExpressionSyntax anonymous:
            {
                open = anonymous.OpenBraceToken;
                close = anonymous.CloseBraceToken;
                break;
            }

            default:
            {
                open = default;
                close = default;
                return false;
            }
        }

        return !open.IsKind(SyntaxKind.None) && !close.IsKind(SyntaxKind.None) && !open.IsMissing && !close.IsMissing;
    }

    /// <summary>The brace-bearing node kinds inspected by the brace-placement rules.</summary>
    /// <returns>The handled node kinds.</returns>
    public static ImmutableArray<SyntaxKind> BraceBearingKinds() => ImmutableArrays.Of(
        SyntaxKind.Block,
        SyntaxKind.AccessorList,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.NamespaceDeclaration,
        SyntaxKind.SwitchStatement,
        SyntaxKind.SwitchExpression,
        SyntaxKind.ObjectInitializerExpression,
        SyntaxKind.ArrayInitializerExpression,
        SyntaxKind.CollectionInitializerExpression,
        SyntaxKind.ComplexElementInitializerExpression,
        SyntaxKind.WithInitializerExpression,
        SyntaxKind.AnonymousObjectCreationExpression);

    /// <summary>Returns the embedded statement of the branch/loop control kinds, or <see langword="null"/>.</summary>
    /// <param name="node">The control-flow node.</param>
    /// <returns>The embedded statement, or <see langword="null"/>.</returns>
    private static StatementSyntax? EmbeddedStatementOrNull(SyntaxNode node) => node switch
    {
        IfStatementSyntax @if => @if.Statement,
        ElseClauseSyntax @else => @else.Statement,
        CommonForEachStatementSyntax forEach => forEach.Statement,
        ForStatementSyntax @for => @for.Statement,
        WhileStatementSyntax @while => @while.Statement,
        _ => null
    };

    /// <summary>Returns the embedded statement of the resource/do control kinds, or <see langword="null"/>.</summary>
    /// <param name="node">The control-flow node.</param>
    /// <returns>The embedded statement, or <see langword="null"/>.</returns>
    private static StatementSyntax? SecondaryEmbeddedStatementOrNull(SyntaxNode node) => node switch
    {
        DoStatementSyntax @do => @do.Statement,
        UsingStatementSyntax @using => @using.Statement,
        LockStatementSyntax @lock => @lock.Statement,
        FixedStatementSyntax @fixed => @fixed.Statement,
        _ => null
    };

    /// <summary>Summarizes how a token relates to other tokens on its line.</summary>
    /// <param name="StartsLine">Whether the token is the first token on its line.</param>
    /// <param name="SharesLineWithPrevious">Whether code precedes the token on its line.</param>
    /// <param name="SharesLineWithNext">Whether code follows the token on its line.</param>
    public readonly record struct TokenLineFacts(bool StartsLine, bool SharesLineWithPrevious, bool SharesLineWithNext);
}
