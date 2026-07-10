// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>Reads the numeric literals that SST1471 accepts without a name.</summary>
internal static class MagicNumberOptions
{
    /// <summary>The rule-specific option key.</summary>
    public const string RuleKey = "stylesharp.SST1471.magic_number_allowed_values";

    /// <summary>The project-wide option key.</summary>
    public const string GeneralKey = "stylesharp.magic_number_allowed_values";

    /// <summary>The values a literal may take without naming it: not-found, empty and first.</summary>
    private static readonly decimal[] DefaultAllowed = [-1m, 0m, 1m];

    /// <summary>Reads the configured allow-list, falling back to <c>-1</c>, <c>0</c> and <c>1</c>.</summary>
    /// <param name="options">The analyzer config options for the literal's tree.</param>
    /// <returns>The allowed values, in configuration order.</returns>
    /// <remarks>
    /// An unset, empty or wholly unparsable option yields the default set rather than an empty one, so a
    /// typo relaxes nothing and never silently turns every literal into a diagnostic.
    /// </remarks>
    public static decimal[] Read(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(RuleKey, out var value) && !options.TryGetValue(GeneralKey, out value))
        {
            return DefaultAllowed;
        }

        return Parse(value) ?? DefaultAllowed;
    }

    /// <summary>Returns whether a value is present in the allow-list.</summary>
    /// <param name="allowed">The allowed values.</param>
    /// <param name="value">The literal's value.</param>
    /// <returns><see langword="true"/> when the value needs no name.</returns>
    public static bool Contains(decimal[] allowed, decimal value)
    {
        for (var i = 0; i < allowed.Length; i++)
        {
            if (allowed[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses a comma-separated list of numbers.</summary>
    /// <param name="value">The raw option value.</param>
    /// <returns>The parsed values, or <see langword="null"/> when none parsed.</returns>
    private static decimal[]? Parse(string value)
    {
        var parts = value.Split(',');
        var parsed = new decimal[parts.Length];
        var count = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            if (decimal.TryParse(parts[i].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                parsed[count++] = number;
            }
        }

        if (count == 0)
        {
            return null;
        }

        if (count == parsed.Length)
        {
            return parsed;
        }

        var trimmed = new decimal[count];
        Array.Copy(parsed, trimmed, count);
        return trimmed;
    }
}
