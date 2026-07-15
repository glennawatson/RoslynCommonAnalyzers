// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads and rewrites custom date/time format strings for SST2445. A custom format treats an unquoted
/// <c>/</c> as the culture's date separator and an unquoted <c>:</c> as its time separator; both helpers
/// respect single-quote and double-quote literal sections and the <c>\</c> escape, so a separator that is
/// already quoted is neither reported nor doubly quoted.
/// </summary>
internal static class DateFormatText
{
    /// <summary>The length of a backslash escape sequence.</summary>
    private const int EscapeSequenceLength = 2;

    /// <summary>Returns whether a custom format contains an unquoted date or time separator.</summary>
    /// <param name="format">The custom format string.</param>
    /// <returns><see langword="true"/> when an unquoted <c>/</c> or <c>:</c> is present.</returns>
    public static bool HasUnquotedSeparator(string format)
    {
        var quote = '\0';
        var i = 0;
        while (i < format.Length)
        {
            var c = format[i];
            if (c == '\\')
            {
                i += EscapeSequenceLength;
                continue;
            }

            if (quote == '\0' && c is '/' or ':')
            {
                return true;
            }

            quote = NextQuoteState(quote, c);
            i++;
        }

        return false;
    }

    /// <summary>Wraps each unquoted separator in a custom format in single quotes so it stays literal.</summary>
    /// <param name="format">The custom format string.</param>
    /// <returns>The format with its separators quoted.</returns>
    public static string QuoteSeparators(string format)
    {
        var builder = new StringBuilder(format.Length);
        var quote = '\0';
        var i = 0;
        while (i < format.Length)
        {
            var c = format[i];
            if (c == '\\')
            {
                AppendEscape(builder, format, i);
                i += EscapeSequenceLength;
                continue;
            }

            if (quote == '\0' && c is '/' or ':')
            {
                builder.Append('\'').Append(c).Append('\'');
            }
            else
            {
                quote = NextQuoteState(quote, c);
                builder.Append(c);
            }

            i++;
        }

        return builder.ToString();
    }

    /// <summary>Appends a backslash escape, keeping the backslash and the character it escapes.</summary>
    /// <param name="builder">The output builder.</param>
    /// <param name="format">The custom format string.</param>
    /// <param name="index">The index of the backslash.</param>
    private static void AppendEscape(StringBuilder builder, string format, int index)
    {
        builder.Append('\\');
        if (index + 1 >= format.Length)
        {
            return;
        }

        builder.Append(format[index + 1]);
    }

    /// <summary>Advances the quote state for one character outside a separator.</summary>
    /// <param name="quote">The open quote character, or the null character when outside a quote.</param>
    /// <param name="c">The character being read.</param>
    /// <returns>The next quote state.</returns>
    private static char NextQuoteState(char quote, char c)
    {
        if (quote != '\0')
        {
            return c == quote ? '\0' : quote;
        }

        return c is '\'' or '"' ? c : '\0';
    }
}
