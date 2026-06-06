// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Shared, allocation-free helpers for the tuple rules (SST1141/SST1142/SST1414).</summary>
internal static class TupleHelpers
{
    /// <summary>The prefix of the positional tuple field names (<c>Item1</c>, <c>Item2</c>, …).</summary>
    private const string ItemPrefix = "Item";

    /// <summary>The base used when accumulating the digits of a tuple position.</summary>
    private const int DecimalBase = 10;

    /// <summary>
    /// Parses a positional tuple field name of the form <c>ItemN</c> into its 1-based position
    /// without allocating (no substring or parse). Returns <see langword="false"/> for any other name.
    /// </summary>
    /// <param name="name">The accessed member name.</param>
    /// <param name="position">The 1-based tuple position when the name is <c>ItemN</c>.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> is <c>Item</c> followed by digits.</returns>
    public static bool TryGetItemPosition(string name, out int position)
    {
        position = 0;
        if (name.Length <= ItemPrefix.Length || !name.StartsWith(ItemPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var value = 0;
        for (var i = ItemPrefix.Length; i < name.Length; i++)
        {
            var digit = name[i];
            if (digit is < '0' or > '9')
            {
                return false;
            }

            value = (value * DecimalBase) + (digit - '0');
        }

        position = value;
        return value > 0;
    }
}
