// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Allocation-free comparisons of a region of a decoded literal against lower-case keywords, shared by the
/// literal classifiers that match keys and placeholder words without taking substrings
/// (<see cref="EmptyConnectionStringPasswordClassifier"/>, <see cref="HardcodedSecretClassifier"/>). Only ASCII
/// upper-case letters in the region are folded; every other character must match exactly.
/// </summary>
internal static class AsciiText
{
    /// <summary>Returns whether a region equals a lower-case word, folding ASCII letters in the region.</summary>
    /// <param name="value">The text the region indexes into.</param>
    /// <param name="start">The inclusive start of the region.</param>
    /// <param name="end">The exclusive end of the region.</param>
    /// <param name="lowercaseWord">The lower-case word to compare against.</param>
    /// <returns><see langword="true"/> when the region equals the word.</returns>
    internal static bool RegionEqualsLowercase(string value, int start, int end, string lowercaseWord)
    {
        if (end - start != lowercaseWord.Length)
        {
            return false;
        }

        for (var i = 0; i < lowercaseWord.Length; i++)
        {
            if (ToLower(value[start + i]) != lowercaseWord[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a region equals any of a set of lower-case words, folding ASCII letters in the region.</summary>
    /// <param name="value">The text the region indexes into.</param>
    /// <param name="start">The inclusive start of the region.</param>
    /// <param name="end">The exclusive end of the region.</param>
    /// <param name="lowercaseWords">The lower-case words to compare against.</param>
    /// <returns><see langword="true"/> when the region equals one of the words.</returns>
    internal static bool RegionEqualsAnyLowercase(string value, int start, int end, string[] lowercaseWords)
    {
        for (var i = 0; i < lowercaseWords.Length; i++)
        {
            if (RegionEqualsLowercase(value, start, end, lowercaseWords[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lower-cases an ASCII letter, leaving every other character untouched.</summary>
    /// <param name="c">The character to fold.</param>
    /// <returns>The lower-cased character.</returns>
    private static char ToLower(char c) =>
        c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c;
}
