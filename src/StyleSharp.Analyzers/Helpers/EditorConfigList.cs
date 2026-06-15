// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Parses the comma-, semicolon-, or whitespace-separated list values used by
/// <c>.editorconfig</c> options without allocating intermediate substrings.
/// </summary>
internal static class EditorConfigList
{
    /// <summary>Returns whether a separated list value contains a token.</summary>
    /// <param name="list">The raw list value (may be empty).</param>
    /// <param name="token">The token to find.</param>
    /// <param name="comparison">The comparison used to match a token.</param>
    /// <returns><see langword="true"/> when the list contains the token.</returns>
    public static bool Contains(string list, string token, StringComparison comparison)
    {
        var start = 0;
        for (var i = 0; i <= list.Length; i++)
        {
            if (i != list.Length && !IsSeparator(list[i]))
            {
                continue;
            }

            var sliceStart = start;
            var sliceEnd = i;
            while (sliceStart < sliceEnd && char.IsWhiteSpace(list[sliceStart]))
            {
                sliceStart++;
            }

            while (sliceEnd > sliceStart && char.IsWhiteSpace(list[sliceEnd - 1]))
            {
                sliceEnd--;
            }

            var length = sliceEnd - sliceStart;
            if (length == token.Length && string.Compare(list, sliceStart, token, 0, length, comparison) == 0)
            {
                return true;
            }

            start = i + 1;
        }

        return false;
    }

    /// <summary>Returns whether the value for a key is present and contains a token.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <param name="key">The editorconfig key to read.</param>
    /// <param name="token">The token to find.</param>
    /// <param name="comparison">The comparison used to match a token.</param>
    /// <returns><see langword="true"/> when the key is present and its list contains the token.</returns>
    public static bool ContainsToken(AnalyzerConfigOptions options, string key, string token, StringComparison comparison)
        => options.TryGetValue(key, out var list) && list.Length != 0 && Contains(list, token, comparison);

    /// <summary>Returns whether a character separates list entries.</summary>
    /// <param name="value">The character to test.</param>
    /// <returns><see langword="true"/> for a comma, semicolon, space, or tab.</returns>
    private static bool IsSeparator(char value) => value is ',' or ';' or ' ' or '\t';
}
