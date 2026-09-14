// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Searches a name or a literal for any of a curated set of fragments without allocating, shared by the rules
/// that recognize a secret-shaped name or a policy token by substring (SES1005, SES1009, SES1515).
/// </summary>
internal static class TextFragments
{
    /// <summary>Returns whether a text contains any of a set of fragments under a comparison.</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="fragments">The fragments to look for.</param>
    /// <param name="comparison">How the text and each fragment are compared.</param>
    /// <returns><see langword="true"/> when at least one fragment occurs in the text.</returns>
    internal static bool ContainsAny(string text, string[] fragments, StringComparison comparison)
    {
        for (var i = 0; i < fragments.Length; i++)
        {
            if (text.IndexOf(fragments[i], comparison) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
