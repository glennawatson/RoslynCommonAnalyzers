// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Parses a structured-logging message template into its placeholders in a single index scan, then compares
/// placeholder names without allocating. A placeholder is <c>{name}</c>, with <c>{{</c> and <c>}}</c> read as
/// escaped braces; the name is what remains after an optional leading capturing operator and before any
/// alignment or format suffix. Brace balance is not this parser's concern.
/// </summary>
internal static class LogMessageTemplate
{
    /// <summary>The width of an escaped brace pair (<c>{{</c> or <c>}}</c>).</summary>
    private const int EscapedBraceWidth = 2;

    /// <summary>The shared empty result for a template with no placeholders.</summary>
    private static readonly LogPlaceholder[] None = [];

    /// <summary>Parses a template's value text into its placeholders.</summary>
    /// <param name="text">The template's value text.</param>
    /// <returns>The placeholders, in source order, or an empty array when there are none.</returns>
    public static LogPlaceholder[] Parse(string text)
    {
        var count = Scan(text, null);
        if (count == 0)
        {
            return None;
        }

        var placeholders = new LogPlaceholder[count];
        Scan(text, placeholders);
        return placeholders;
    }

    /// <summary>Returns whether two placeholders carry the same name, compared without regard to case.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="first">The first placeholder.</param>
    /// <param name="second">The second placeholder.</param>
    /// <returns><see langword="true"/> when the names match.</returns>
    public static bool NamesEqual(string text, in LogPlaceholder first, in LogPlaceholder second)
    {
        var length = first.NameLength;
        return length == second.NameLength
            && string.Compare(text, first.NameStart, text, second.NameStart, length, System.StringComparison.OrdinalIgnoreCase) == 0;
    }

    /// <summary>Returns whether a placeholder's name equals a candidate string, compared without regard to case.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="placeholder">The placeholder.</param>
    /// <param name="candidate">The candidate name.</param>
    /// <returns><see langword="true"/> when the name matches the candidate.</returns>
    public static bool NameEquals(string text, in LogPlaceholder placeholder, string candidate)
        => placeholder.NameLength == candidate.Length
            && string.Compare(text, placeholder.NameStart, candidate, 0, candidate.Length, System.StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>Materializes a placeholder's name.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="placeholder">The placeholder.</param>
    /// <returns>The name.</returns>
    public static string GetName(string text, in LogPlaceholder placeholder)
        => text.Substring(placeholder.NameStart, placeholder.NameLength);

    /// <summary>Materializes a placeholder's full text, braces included.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="placeholder">The placeholder.</param>
    /// <returns>The placeholder text.</returns>
    public static string GetText(string text, in LogPlaceholder placeholder)
        => text.Substring(placeholder.ValueStart, placeholder.ValueEnd - placeholder.ValueStart);

    /// <summary>Scans the template, counting placeholders and, when a buffer is supplied, recording them.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="into">The buffer to fill, or <see langword="null"/> to only count.</param>
    /// <returns>The number of placeholders.</returns>
    private static int Scan(string text, LogPlaceholder[]? into)
    {
        var count = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '{')
            {
                i = IsDoubled(text, i) ? i + EscapedBraceWidth : ReadPlaceholder(text, into, ref count, i);
            }
            else if (c == '}')
            {
                i += IsDoubled(text, i) ? EscapedBraceWidth : 1;
            }
            else
            {
                i++;
            }
        }

        return count;
    }

    /// <summary>Reads one placeholder starting at an unescaped opening brace, recording it when a buffer is supplied.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="into">The buffer to fill, or <see langword="null"/> to only count.</param>
    /// <param name="count">The running placeholder count.</param>
    /// <param name="open">The offset of the opening brace.</param>
    /// <returns>The offset to resume scanning from.</returns>
    private static int ReadPlaceholder(string text, LogPlaceholder[]? into, ref int count, int open)
    {
        var contentStart = open + 1;
        if (!TryFindClose(text, contentStart, out var closing))
        {
            return contentStart;
        }

        if (into is not null)
        {
            into[count] = Classify(text, open, closing + 1, contentStart, closing);
        }

        count++;
        return closing + 1;
    }

    /// <summary>Finds the closing brace of a placeholder, stopping if another opening brace intervenes.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="from">The offset to start scanning from.</param>
    /// <param name="closing">The offset of the closing brace.</param>
    /// <returns><see langword="true"/> when a closing brace was found.</returns>
    private static bool TryFindClose(string text, int from, out int closing)
    {
        for (var i = from; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '}')
            {
                closing = i;
                return true;
            }

            if (c == '{')
            {
                break;
            }
        }

        closing = -1;
        return false;
    }

    /// <summary>Returns whether a brace at an offset is doubled, and so escaped.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="index">The offset of the brace.</param>
    /// <returns><see langword="true"/> when the next character repeats the brace.</returns>
    private static bool IsDoubled(string text, int index)
        => index + 1 < text.Length && text[index + 1] == text[index];

    /// <summary>Classifies one placeholder from the span between its braces.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="valueStart">The offset of the opening brace.</param>
    /// <param name="valueEnd">The offset just past the closing brace.</param>
    /// <param name="contentStart">The offset of the first character after the opening brace.</param>
    /// <param name="contentEnd">The offset of the closing brace.</param>
    /// <returns>The classified placeholder.</returns>
    private static LogPlaceholder Classify(string text, int valueStart, int valueEnd, int contentStart, int contentEnd)
    {
        var nameStart = contentStart;
        if (nameStart < contentEnd && text[nameStart] is '@' or '$')
        {
            nameStart++;
        }

        var nameEnd = contentEnd;
        for (var q = nameStart; q < contentEnd; q++)
        {
            if (text[q] is ',' or ':')
            {
                nameEnd = q;
                break;
            }
        }

        var kind = ClassifyName(text, nameStart, nameEnd);
        return new LogPlaceholder(kind, valueStart, valueEnd, nameStart, nameEnd);
    }

    /// <summary>Classifies the name span of a placeholder.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="nameStart">The offset of the name's first character.</param>
    /// <param name="nameEnd">The offset just past the name's last character.</param>
    /// <returns>The placeholder kind.</returns>
    private static LogPlaceholderKind ClassifyName(string text, int nameStart, int nameEnd)
    {
        if (nameEnd == nameStart)
        {
            return LogPlaceholderKind.Malformed;
        }

        var allDigits = true;
        for (var q = nameStart; q < nameEnd; q++)
        {
            var ch = text[q];
            if (!IsNameChar(ch))
            {
                return LogPlaceholderKind.Malformed;
            }

            if (!IsDigit(ch))
            {
                allDigits = false;
            }
        }

        return allDigits ? LogPlaceholderKind.Numeric : LogPlaceholderKind.Named;
    }

    /// <summary>Returns whether a character can appear in a placeholder name.</summary>
    /// <param name="ch">The character.</param>
    /// <returns><see langword="true"/> when the character is a letter, digit, or underscore.</returns>
    private static bool IsNameChar(char ch)
        => ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';

    /// <summary>Returns whether a character is a digit.</summary>
    /// <param name="ch">The character.</param>
    /// <returns><see langword="true"/> when the character is a digit.</returns>
    private static bool IsDigit(char ch) => ch is >= '0' and <= '9';
}
