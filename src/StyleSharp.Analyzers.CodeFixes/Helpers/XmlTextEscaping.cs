// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>Escapes the characters that close or corrupt the character data of a documentation comment.</summary>
/// <remarks>The ampersand is escaped in the same pass as the brackets, so the entities written are never escaped twice.</remarks>
internal static class XmlTextEscaping
{
    /// <summary>Escapes a string.</summary>
    /// <param name="text">The text to escape.</param>
    /// <returns><paramref name="text"/> itself when nothing needs escaping, otherwise the escaped copy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Escape(string text) =>
        text.AsSpan().IndexOfAny('&', '<', '>') < 0 ? text : EscapeAll(text.AsSpan());

    /// <summary>Escapes a run of characters.</summary>
    /// <param name="value">The characters to escape.</param>
    /// <returns>The escaped text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Escape(ReadOnlySpan<char> value) =>
        value.IndexOfAny('&', '<', '>') < 0 ? value.ToString() : EscapeAll(value);

    /// <summary>Writes every character, replacing the XML-significant ones with their entities.</summary>
    /// <param name="value">The characters to escape.</param>
    /// <returns>The escaped text.</returns>
    private static string EscapeAll(ReadOnlySpan<char> value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '&':
                {
                    _ = builder.Append("&amp;");
                    break;
                }

                case '<':
                {
                    _ = builder.Append("&lt;");
                    break;
                }

                case '>':
                {
                    _ = builder.Append("&gt;");
                    break;
                }

                default:
                {
                    _ = builder.Append(character);
                    break;
                }
            }
        }

        return builder.ToString();
    }
}
