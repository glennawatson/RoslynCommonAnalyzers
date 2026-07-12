// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1473 settings for one syntax tree.</summary>
/// <param name="AllowZeroComparison">Whether a comparison against a literal zero is left alone.</param>
internal readonly record struct FloatingPointComparisonOptions(bool AllowZeroComparison)
{
    /// <summary>Zero comparisons are allowed unless the configuration says otherwise.</summary>
    public const bool DefaultAllowZeroComparison = true;

    /// <summary>The rule-specific zero-comparison key.</summary>
    private const string AllowZeroRuleKey = "stylesharp.SST1473.allow_zero_comparison";

    /// <summary>The project-wide zero-comparison key.</summary>
    private const string AllowZeroGeneralKey = "stylesharp.allow_zero_comparison";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the comparison's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// An unset or unparsable value yields the default, so a typo neither turns the rule off nor starts
    /// reporting every <c>x == 0</c> in the file.
    /// </remarks>
    public static FloatingPointComparisonOptions Read(AnalyzerConfigOptions options)
        => new(ReadBool(options, AllowZeroRuleKey, AllowZeroGeneralKey, DefaultAllowZeroComparison));

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
