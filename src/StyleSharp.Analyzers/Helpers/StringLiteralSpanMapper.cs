// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Maps a span within a string literal's decoded value back to the corresponding span in source, so a
/// diagnostic can point at a placeholder inside the literal rather than at the whole literal. Regular and
/// verbatim literals are mapped exactly by decoding as the walk proceeds; a raw literal — where the mapping is
/// not worth reconstructing — falls back to the whole literal.
/// </summary>
internal static class StringLiteralSpanMapper
{
    /// <summary>The raw length of a two-character escape or a doubled quote.</summary>
    private const int PairLength = 2;

    /// <summary>The raw length of a <c>\uXXXX</c> escape.</summary>
    private const int Utf16EscapeLength = 6;

    /// <summary>The raw length of a <c>\UXXXXXXXX</c> escape.</summary>
    private const int Utf32EscapeLength = 10;

    /// <summary>The most hexadecimal digits a <c>\x</c> escape reads.</summary>
    private const int MaxShortHexDigits = 4;

    /// <summary>Maps a value-text span to its source span within a literal.</summary>
    /// <param name="literal">The string-literal expression.</param>
    /// <param name="valueStart">The start offset in the literal's decoded value.</param>
    /// <param name="valueLength">The length in the literal's decoded value.</param>
    /// <param name="span">The source span, when it can be mapped.</param>
    /// <returns><see langword="true"/> when the span was mapped; otherwise the caller should use the whole literal.</returns>
    public static bool TryMap(LiteralExpressionSyntax literal, int valueStart, int valueLength, out TextSpan span)
    {
        span = default;
        var token = literal.Token;
        var text = token.Text;
        if (text.Length == 0)
        {
            return false;
        }

        var verbatim = text[0] == '@';
        var quoteIndex = verbatim ? 1 : 0;
        if (quoteIndex >= text.Length || text[quoteIndex] != '"')
        {
            return false;
        }

        // A raw literal opens with three quotes; its mapping is not reconstructed here.
        var contentStart = quoteIndex + 1;
        if (!verbatim && contentStart < text.Length && text[contentStart] == '"')
        {
            return false;
        }

        if (!TryAdvance(text, contentStart, verbatim, valueStart, out var sourceStart)
            || !TryAdvance(text, sourceStart, verbatim, valueLength, out var sourceEnd))
        {
            return false;
        }

        span = TextSpan.FromBounds(token.SpanStart + sourceStart, token.SpanStart + sourceEnd);
        return true;
    }

    /// <summary>Advances a number of decoded characters through the literal text.</summary>
    /// <param name="text">The literal's raw text.</param>
    /// <param name="from">The starting index in the raw text.</param>
    /// <param name="verbatim">Whether the literal is verbatim.</param>
    /// <param name="valueCount">The number of decoded characters to advance.</param>
    /// <param name="result">The resulting index in the raw text.</param>
    /// <returns><see langword="true"/> when the advance stayed within the text.</returns>
    private static bool TryAdvance(string text, int from, bool verbatim, int valueCount, out int result)
    {
        var index = from;
        for (var consumed = 0; consumed < valueCount; consumed++)
        {
            if (index >= text.Length)
            {
                result = index;
                return false;
            }

            index += StepLength(text, index, verbatim);
        }

        result = index;
        return true;
    }

    /// <summary>Returns the raw length that encodes one decoded character at an index.</summary>
    /// <param name="text">The literal's raw text.</param>
    /// <param name="index">The index in the raw text.</param>
    /// <param name="verbatim">Whether the literal is verbatim.</param>
    /// <returns>The number of raw characters to skip.</returns>
    private static int StepLength(string text, int index, bool verbatim)
        => verbatim ? VerbatimStepLength(text, index) : RegularStepLength(text, index);

    /// <summary>Returns the raw length that encodes one decoded character in a verbatim literal.</summary>
    /// <param name="text">The literal's raw text.</param>
    /// <param name="index">The index in the raw text.</param>
    /// <returns>The number of raw characters to skip.</returns>
    private static int VerbatimStepLength(string text, int index)
        => text[index] == '"' && index + 1 < text.Length && text[index + 1] == '"' ? PairLength : 1;

    /// <summary>Returns the raw length that encodes one decoded character in a regular literal.</summary>
    /// <param name="text">The literal's raw text.</param>
    /// <param name="index">The index in the raw text.</param>
    /// <returns>The number of raw characters to skip.</returns>
    private static int RegularStepLength(string text, int index)
    {
        if (text[index] != '\\' || index + 1 >= text.Length)
        {
            return 1;
        }

        return text[index + 1] switch
        {
            'u' => Utf16EscapeLength,
            'x' => PairLength + CountHexDigits(text, index + PairLength, MaxShortHexDigits),
            'U' => Utf32EscapeLength,
            _ => PairLength,
        };
    }

    /// <summary>Counts consecutive hexadecimal digits, up to a maximum.</summary>
    /// <param name="text">The literal's raw text.</param>
    /// <param name="start">The first index to examine.</param>
    /// <param name="max">The most digits to count.</param>
    /// <returns>The number of hexadecimal digits.</returns>
    private static int CountHexDigits(string text, int start, int max)
    {
        var count = 0;
        while (count < max && start + count < text.Length && IsHexDigit(text[start + count]))
        {
            count++;
        }

        return count;
    }

    /// <summary>Returns whether a character is a hexadecimal digit.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> when the character is a hexadecimal digit.</returns>
    private static bool IsHexDigit(char c)
        => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
