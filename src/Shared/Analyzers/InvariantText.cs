// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Culture-invariant text comparisons over spans, so a match never has to lower-case a copy first.</summary>
internal static class InvariantText
{
    /// <summary>Returns whether text lower-cases, character by character under the invariant culture, to a lowercase word.</summary>
    /// <param name="text">The text to compare.</param>
    /// <param name="lowercase">The expected word, already lowercase.</param>
    /// <returns><see langword="true"/> when <c>ToLowerInvariant</c> of every character of <paramref name="text"/> yields <paramref name="lowercase"/>.</returns>
    internal static bool EqualsLowercase(ReadOnlySpan<char> text, string lowercase)
    {
        if (text.Length != lowercase.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (char.ToLowerInvariant(text[i]) != lowercase[i])
            {
                return false;
            }
        }

        return true;
    }
}
