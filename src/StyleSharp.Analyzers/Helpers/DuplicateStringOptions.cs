// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1486 settings for one syntax tree.</summary>
/// <param name="Threshold">The number of copies of one literal at which the file is reported.</param>
/// <param name="MinimumLength">The shortest literal that is worth naming.</param>
internal readonly record struct DuplicateStringOptions(int Threshold, int MinimumLength)
{
    /// <summary>The default number of copies that triggers a report.</summary>
    public const int DefaultThreshold = 3;

    /// <summary>The default shortest literal that counts.</summary>
    public const int DefaultMinimumLength = 5;

    /// <summary>The smallest threshold that means anything: one copy is not a duplicate.</summary>
    private const int SmallestThreshold = 2;

    /// <summary>The smallest length that means anything: the empty string is excluded outright.</summary>
    private const int SmallestLength = 1;

    /// <summary>The rule-specific threshold key.</summary>
    private const string ThresholdRuleKey = "stylesharp.SST1486.duplicate_string_threshold";

    /// <summary>The project-wide threshold key.</summary>
    private const string ThresholdGeneralKey = "stylesharp.duplicate_string_threshold";

    /// <summary>The rule-specific minimum-length key.</summary>
    private const string MinimumLengthRuleKey = "stylesharp.SST1486.minimum_string_length";

    /// <summary>The project-wide minimum-length key.</summary>
    private const string MinimumLengthGeneralKey = "stylesharp.minimum_string_length";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the literal's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// A value below the smallest sensible one is treated as unset. A threshold of 1 would report every
    /// literal in the file and a length of 0 would report every <c>""</c>, so a typo there must not become a
    /// rule that shouts at everything.
    /// </remarks>
    public static DuplicateStringOptions Read(AnalyzerConfigOptions options) => new(
        ReadCount(options, ThresholdRuleKey, ThresholdGeneralKey, SmallestThreshold, DefaultThreshold),
        ReadCount(options, MinimumLengthRuleKey, MinimumLengthGeneralKey, SmallestLength, DefaultMinimumLength));

    /// <summary>Reads a bounded integer setting, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="smallest">The smallest value that is accepted.</param>
    /// <param name="fallback">The value used when neither key parses.</param>
    /// <returns>The configured value, or <paramref name="fallback"/>.</returns>
    private static int ReadCount(AnalyzerConfigOptions options, string ruleKey, string generalKey, int smallest, int fallback)
    {
        if (options.TryGetValue(ruleKey, out var value) && int.TryParse(value, out var parsed) && parsed >= smallest)
        {
            return parsed;
        }

        return options.TryGetValue(generalKey, out value) && int.TryParse(value, out parsed) && parsed >= smallest
            ? parsed
            : fallback;
    }
}
