// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Renders the inner text of a <c>&lt;summary&gt;</c> as it appears once collapsed onto one line:
/// the <c>///</c> exteriors dropped, runs of whitespace reduced to single spaces, the ends trimmed.
/// </summary>
/// <remarks>
/// SST1653's analyzer has to predict the exact line its own code fix would produce, so both sides
/// measure and build through this one routine. A second implementation would drift, and a drifted
/// prediction is a rule that reports a rewrite the layout rules then reject.
/// </remarks>
internal static class SummaryCollapse
{
    /// <summary>The documentation exterior that prefixes each line of a summary.</summary>
    private const string Exterior = "///";

    /// <summary>Returns the length of the collapsed inner text without allocating.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="innerSpan">The raw span between the summary tags.</param>
    /// <returns>The number of characters the collapsed text occupies.</returns>
    public static int CollapsedLength(SourceText text, TextSpan innerSpan) => Render(text, innerSpan, builder: null);

    /// <summary>Returns the collapsed inner text.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="innerSpan">The raw span between the summary tags.</param>
    /// <returns>The single-line inner text.</returns>
    public static string Collapse(SourceText text, TextSpan innerSpan)
    {
        var builder = new StringBuilder(innerSpan.Length);
        Render(text, innerSpan, builder);
        return builder.ToString();
    }

    /// <summary>Walks the raw span, optionally writing the collapsed text, and returns its length.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="innerSpan">The raw span between the summary tags.</param>
    /// <param name="builder">The destination, or <see langword="null"/> to measure only.</param>
    /// <returns>The number of characters written, or that would be written.</returns>
    private static int Render(SourceText text, TextSpan innerSpan, StringBuilder? builder)
    {
        var length = 0;
        var started = false;
        var pendingSpace = false;
        var i = innerSpan.Start;
        var end = innerSpan.End;

        while (i < end)
        {
            // Skip the '///' documentation exterior that prefixes each line.
            if (StartsWith(text, i, end, Exterior))
            {
                i += Exterior.Length;
                continue;
            }

            var character = text[i];
            i++;

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = started;
                continue;
            }

            if (pendingSpace)
            {
                builder?.Append(' ');
                length++;
                pendingSpace = false;
            }

            builder?.Append(character);
            length++;
            started = true;
        }

        return length;
    }

    /// <summary>Returns whether <paramref name="text"/> contains <paramref name="value"/> starting at <paramref name="index"/>.</summary>
    /// <param name="text">The text to test.</param>
    /// <param name="index">The position to test at.</param>
    /// <param name="end">The exclusive end position of the tested span.</param>
    /// <param name="value">The substring to look for.</param>
    /// <returns><see langword="true"/> on a match.</returns>
    private static bool StartsWith(SourceText text, int index, int end, string value)
    {
        if (index + value.Length > end)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (text[index + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }
}
