// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Allocation-free lookups over the small fixed name tables analyzers keep in static arrays.</summary>
internal static class StringArrays
{
    /// <summary>Returns whether a table contains a value, compared ordinally.</summary>
    /// <param name="values">The table to search.</param>
    /// <param name="value">The value to find.</param>
    /// <returns><see langword="true"/> when an entry equals <paramref name="value"/> ordinally.</returns>
    internal static bool ContainsOrdinal(string[] values, string value)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (string.Equals(value, values[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
