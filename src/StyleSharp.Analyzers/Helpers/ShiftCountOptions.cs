// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1478 settings for one syntax tree.</summary>
/// <param name="AllowZeroShift">Whether a shift by a constant zero is allowed.</param>
internal readonly record struct ShiftCountOptions(bool AllowZeroShift)
{
    /// <summary>The rule-specific zero-shift key.</summary>
    private const string AllowZeroShiftRuleKey = "stylesharp.SST1478.allow_zero_shift";

    /// <summary>The project-wide zero-shift key.</summary>
    private const string AllowZeroShiftGeneralKey = "stylesharp.allow_zero_shift";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the shift's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// A shift by zero is reported by default: it does nothing, and the count it was meant to be is
    /// usually one off. Set the key to <c>true</c> where a zero is written deliberately to keep a table of
    /// shift distances lined up. An unset or unparsable value keeps the default, so a typo does not
    /// silently turn half the rule off.
    /// </remarks>
    public static ShiftCountOptions Read(AnalyzerConfigOptions options)
        => new(ReadBool(options, AllowZeroShiftRuleKey, AllowZeroShiftGeneralKey, fallback: false));

    /// <summary>Reads a boolean setting, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="fallback">The value used when neither key parses.</param>
    /// <returns>The configured value, or <paramref name="fallback"/>.</returns>
    private static bool ReadBool(AnalyzerConfigOptions options, string ruleKey, string generalKey, bool fallback)
    {
        if (options.TryGetValue(ruleKey, out var value) && bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return options.TryGetValue(generalKey, out value) && bool.TryParse(value, out parsed)
            ? parsed
            : fallback;
    }
}
