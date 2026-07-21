// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the physical-line layout of a canonical, pure-prose <c>&lt;summary&gt;</c> for SST1664 — the inner
/// line range, whether the tags sit alone on their lines, and whether blank documentation lines separate
/// prose paragraphs. Shared so the analyzer and its code fix agree on the exact shape they act on.
/// </summary>
internal static class SummaryParagraphLayout
{
    /// <summary>The minimum number of lines the summary must span for at least one content line to sit between the tags.</summary>
    private const int MinimumTagLineGap = 2;

    /// <summary>
    /// Returns the inner (content) line range of a summary whose <c>&lt;summary&gt;</c> and
    /// <c>&lt;/summary&gt;</c> tags each sit alone on their line and which spans more than one line.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="element">The summary element.</param>
    /// <param name="firstInnerLine">The first content line number when the method returns <see langword="true"/>.</param>
    /// <param name="lastInnerLine">The last content line number when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for a canonical multi-line summary with at least one inner line.</returns>
    public static bool TryGetInnerLineRange(SourceText text, XmlElementSyntax element, out int firstInnerLine, out int lastInnerLine)
    {
        firstInnerLine = 0;
        lastInnerLine = 0;

        var startTagLine = text.Lines.GetLineFromPosition(element.StartTag.Span.End).LineNumber;
        var endTagLine = text.Lines.GetLineFromPosition(element.EndTag.Span.Start).LineNumber;
        if (endTagLine - startTagLine < MinimumTagLineGap)
        {
            // Single line, or no content line between the tags.
            return false;
        }

        if (!IsWhitespaceRange(text, element.StartTag.Span.End, text.Lines[startTagLine].End)
            || !IsDocumentationExteriorRange(text, text.Lines[endTagLine].Start, element.EndTag.Span.Start))
        {
            // A tag shares its line with prose; not the canonical layout the fix rewrites.
            return false;
        }

        firstInnerLine = startTagLine + 1;
        lastInnerLine = endTagLine - 1;
        return true;
    }

    /// <summary>Returns whether the inner lines contain prose, then a blank documentation line, then prose again.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="firstInnerLine">The first inner line number.</param>
    /// <param name="lastInnerLine">The last inner line number.</param>
    /// <returns><see langword="true"/> when at least two paragraphs are separated by a blank line.</returns>
    public static bool HasBlankSeparatedParagraphs(SourceText text, int firstInnerLine, int lastInnerLine)
    {
        var seenProse = false;
        var pendingBlank = false;
        for (var lineNumber = firstInnerLine; lineNumber <= lastInnerLine; lineNumber++)
        {
            if (LineHasProse(text, text.Lines[lineNumber].Span))
            {
                if (pendingBlank)
                {
                    return true;
                }

                seenProse = true;
            }
            else if (seenProse)
            {
                pendingBlank = true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a documentation line carries prose (content beyond its <c>///</c> exterior).</summary>
    /// <param name="text">The source text.</param>
    /// <param name="span">The line span.</param>
    /// <returns><see langword="true"/> when the line has non-exterior content.</returns>
    public static bool LineHasProse(SourceText text, TextSpan span)
    {
        var i = span.Start;
        while (i < span.End && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        while (i < span.End && text[i] == '/')
        {
            i++;
        }

        while (i < span.End)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return true;
            }

            i++;
        }

        return false;
    }

    /// <summary>Returns whether a half-open range is empty or all whitespace.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <returns><see langword="true"/> when no non-whitespace character is present.</returns>
    private static bool IsWhitespaceRange(SourceText text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a half-open range is only the <c>///</c> exterior (slashes and whitespace).</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <returns><see langword="true"/> when only slashes and whitespace are present.</returns>
    private static bool IsDocumentationExteriorRange(SourceText text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (text[i] != '/' && !char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
