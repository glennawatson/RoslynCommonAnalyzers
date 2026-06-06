// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared, stateless helpers for the layout (SST15xx) code fixes. Each member is
/// <see langword="static"/> and works on the document's <see cref="SourceText"/>, so the
/// fixes reuse one implementation of newline detection, indentation, and whitespace probing.
/// </summary>
internal static class LayoutFixHelpers
{
    /// <summary>One level of indentation (the repository uses four-space indents).</summary>
    public const string IndentStep = "    ";

    /// <summary>The character length of a CR/LF newline.</summary>
    private const int CrLfLength = 2;

    /// <summary>Returns the newline sequence used by the document (defaults to "\n" when none is found).</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The detected newline string.</returns>
    public static string DetectNewLine(SourceText text)
    {
        if (text.Lines.Count == 0)
        {
            return "\n";
        }

        var firstLine = text.Lines[0];
        return firstLine.EndIncludingLineBreak - firstLine.End == CrLfLength ? "\r\n" : "\n";
    }

    /// <summary>Returns the leading-whitespace indent of the line containing <paramref name="position"/>.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">A position on the target line.</param>
    /// <returns>The indentation string.</returns>
    public static string IndentOfLine(SourceText text, int position)
    {
        var line = text.Lines.GetLineFromPosition(position);
        var end = line.Start;
        while (end < line.End && char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return text.ToString(TextSpan.FromBounds(line.Start, end));
    }

    /// <summary>Returns whether the half-open span contains only whitespace (safe to rewrite).</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <returns><see langword="true"/> when no non-whitespace character lies between the bounds.</returns>
    public static bool IsWhitespaceBetween(SourceText text, int start, int end)
    {
        for (var position = start; position < end; position++)
        {
            if (!char.IsWhiteSpace(text[position]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Appends the line breaks that spread a single-line block's statements and closing brace across lines.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="block">The block to expand.</param>
    /// <param name="newLine">The document newline sequence.</param>
    /// <param name="changes">The change set to append to.</param>
    /// <returns><see langword="true"/> when the rewrite is safe; <see langword="false"/> when a comment blocks it.</returns>
    public static bool TryAppendBlockExpansion(SourceText text, BlockSyntax block, string newLine, List<TextChange> changes)
    {
        var openBrace = block.OpenBraceToken;
        var ownerIndent = IndentOfLine(text, openBrace.SpanStart);
        var childIndent = ownerIndent + IndentStep;
        var braceLine = text.Lines.GetLineFromPosition(openBrace.SpanStart).LineNumber;

        // Move the opening brace onto its own line when it shares the line with earlier code.
        var previous = openBrace.GetPreviousToken();
        if (!previous.IsKind(SyntaxKind.None)
            && text.Lines.GetLineFromPosition(previous.Span.End - 1).LineNumber == braceLine
            && IsWhitespaceBetween(text, previous.Span.End, openBrace.SpanStart))
        {
            changes.Add(new TextChange(TextSpan.FromBounds(previous.Span.End, openBrace.SpanStart), newLine + ownerIndent));
        }

        var cursor = openBrace.Span.End;
        foreach (var statement in block.Statements)
        {
            if (!IsWhitespaceBetween(text, cursor, statement.SpanStart))
            {
                return false;
            }

            changes.Add(new TextChange(TextSpan.FromBounds(cursor, statement.SpanStart), newLine + childIndent));
            cursor = statement.Span.End;
        }

        if (!IsWhitespaceBetween(text, cursor, block.CloseBraceToken.SpanStart))
        {
            return false;
        }

        changes.Add(new TextChange(TextSpan.FromBounds(cursor, block.CloseBraceToken.SpanStart), newLine + ownerIndent));
        return true;
    }

    /// <summary>Appends the two inserts that wrap a bare embedded statement in braces on their own lines.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="statement">The bare statement to wrap.</param>
    /// <param name="newLine">The document newline sequence.</param>
    /// <param name="changes">The change set to append to.</param>
    public static void AppendBraceWrap(SourceText text, StatementSyntax statement, string newLine, List<TextChange> changes)
    {
        var ownerIndent = IndentOfLine(text, statement.Parent!.GetFirstToken().SpanStart);
        var childIndent = ownerIndent + IndentStep;
        var header = statement.GetFirstToken().GetPreviousToken();
        if (!IsWhitespaceBetween(text, header.Span.End, statement.SpanStart))
        {
            return;
        }

        // Replace the gap before the child with an opening brace on its own line and the child
        // re-indented one level; this handles both single-line and already-multi-line children.
        changes.Add(new TextChange(TextSpan.FromBounds(header.Span.End, statement.SpanStart), newLine + ownerIndent + "{" + newLine + childIndent));
        changes.Add(new TextChange(new TextSpan(statement.Span.End, 0), newLine + ownerIndent + "}"));
    }
}
