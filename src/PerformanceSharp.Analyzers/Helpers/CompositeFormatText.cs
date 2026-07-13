// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Validates a composite format string before PSH1223 offers to hoist it into a
/// <c>CompositeFormat</c> field.
/// </summary>
/// <remarks>
/// The point is not to reimplement the runtime's parser. It is to be sure that a format the rule
/// hoists is one <c>CompositeFormat.Parse</c> will accept, because a format it rejects would throw
/// from a static field initializer — turning a <c>FormatException</c> at the call into a
/// <c>TypeInitializationException</c> somewhere else entirely. So the grammar accepted here is
/// deliberately narrower than the runtime's: the ordinary <c>{index[,alignment][:format]}</c>
/// placeholder, doubled braces as escapes, and nothing else. Anything unusual is refused, which costs
/// a missed report and never a broken fix.
/// </remarks>
internal static class CompositeFormatText
{
    /// <summary>The length of a doubled-brace escape.</summary>
    private const int EscapeLength = 2;

    /// <summary>Returns whether a constant format string is one the rule is willing to hoist.</summary>
    /// <param name="text">The constant format string.</param>
    /// <returns><see langword="true"/> when the format is well-formed and carries at least one placeholder.</returns>
    public static bool IsWellFormed(string text)
    {
        var index = 0;
        var sawPlaceholder = false;
        while (index < text.Length)
        {
            var current = text[index];
            if (current == '}')
            {
                if (!TrySkipEscape(text, ref index, '}'))
                {
                    return false;
                }
            }
            else if (current != '{')
            {
                index++;
            }
            else if (!TrySkipEscape(text, ref index, '{'))
            {
                if (!TryReadPlaceholder(text, ref index))
                {
                    return false;
                }

                sawPlaceholder = true;
            }
        }

        return sawPlaceholder;
    }

    /// <summary>Consumes a doubled brace when the text has one at the current position.</summary>
    /// <param name="text">The format string.</param>
    /// <param name="index">The scan position, advanced past the escape.</param>
    /// <param name="brace">The brace being escaped.</param>
    /// <returns><see langword="true"/> when an escape was consumed.</returns>
    private static bool TrySkipEscape(string text, ref int index, char brace)
    {
        if (index + 1 >= text.Length || text[index + 1] != brace)
        {
            return false;
        }

        index += EscapeLength;
        return true;
    }

    /// <summary>Consumes one <c>{index[,alignment][:format]}</c> placeholder.</summary>
    /// <param name="text">The format string.</param>
    /// <param name="index">The scan position, standing on the opening brace and advanced past the closing one.</param>
    /// <returns><see langword="true"/> when a well-formed placeholder was consumed.</returns>
    private static bool TryReadPlaceholder(string text, ref int index)
    {
        var position = index + 1;
        if (!TryReadDigits(text, ref position))
        {
            return false;
        }

        SkipSpaces(text, ref position);
        if (position < text.Length && text[position] == ',' && !TryReadAlignment(text, ref position))
        {
            return false;
        }

        if (position < text.Length && text[position] == ':' && !TryReadFormatSpecifier(text, ref position))
        {
            return false;
        }

        if (position >= text.Length || text[position] != '}')
        {
            return false;
        }

        index = position + 1;
        return true;
    }

    /// <summary>Consumes a run of at least one decimal digit.</summary>
    /// <param name="text">The format string.</param>
    /// <param name="position">The scan position, advanced past the digits.</param>
    /// <returns><see langword="true"/> when at least one digit was consumed.</returns>
    private static bool TryReadDigits(string text, ref int position)
    {
        var start = position;
        while (position < text.Length && text[position] >= '0' && text[position] <= '9')
        {
            position++;
        }

        return position > start;
    }

    /// <summary>Consumes a <c>,[-]digits</c> alignment clause.</summary>
    /// <param name="text">The format string.</param>
    /// <param name="position">The scan position, standing on the comma and advanced past the clause.</param>
    /// <returns><see langword="true"/> when a well-formed alignment was consumed.</returns>
    private static bool TryReadAlignment(string text, ref int position)
    {
        position++;
        SkipSpaces(text, ref position);
        if (position < text.Length && text[position] == '-')
        {
            position++;
        }

        if (!TryReadDigits(text, ref position))
        {
            return false;
        }

        SkipSpaces(text, ref position);
        return true;
    }

    /// <summary>Consumes a <c>:format</c> clause, refusing any brace inside it.</summary>
    /// <param name="text">The format string.</param>
    /// <param name="position">The scan position, standing on the colon and advanced to the closing brace.</param>
    /// <returns><see langword="true"/> when the clause holds no brace of its own.</returns>
    /// <remarks>
    /// The runtime lets a format specifier escape braces of its own. That is rare enough, and fiddly
    /// enough to get right, that a specifier containing any brace is simply refused rather than
    /// half-understood.
    /// </remarks>
    private static bool TryReadFormatSpecifier(string text, ref int position)
    {
        position++;
        while (position < text.Length && text[position] != '}')
        {
            if (text[position] == '{')
            {
                return false;
            }

            position++;
        }

        return position < text.Length;
    }

    /// <summary>Advances past any run of spaces.</summary>
    /// <param name="text">The format string.</param>
    /// <param name="position">The scan position.</param>
    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && text[position] == ' ')
        {
            position++;
        }
    }
}
