// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1472 settings for one syntax tree.</summary>
/// <param name="Maximum">The most parameters a signature may declare.</param>
/// <param name="CheckPositionalRecords">Whether a positional record's parameter list is measured.</param>
/// <param name="CountOptionalParameters">Whether a parameter with a default value counts toward the total.</param>
internal readonly record struct ParameterCountOptions(
    int Maximum,
    bool CheckPositionalRecords,
    bool CountOptionalParameters)
{
    /// <summary>The default maximum parameter count.</summary>
    public const int DefaultMaximum = 7;

    /// <summary>The rule-specific maximum key.</summary>
    private const string MaximumRuleKey = "stylesharp.SST1472.max_parameters";

    /// <summary>The project-wide maximum key.</summary>
    private const string MaximumGeneralKey = "stylesharp.max_parameters";

    /// <summary>The rule-specific positional-record key.</summary>
    private const string CheckRecordsRuleKey = "stylesharp.SST1472.check_positional_records";

    /// <summary>The project-wide positional-record key.</summary>
    private const string CheckRecordsGeneralKey = "stylesharp.check_positional_records";

    /// <summary>The rule-specific optional-parameter key.</summary>
    private const string CountOptionalRuleKey = "stylesharp.SST1472.count_optional_parameters";

    /// <summary>The project-wide optional-parameter key.</summary>
    private const string CountOptionalGeneralKey = "stylesharp.count_optional_parameters";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the declaration's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// An unset or unparsable value yields the default rather than a permissive or a punitive extreme, so a
    /// typo neither disables the rule nor turns every three-parameter method into a diagnostic.
    /// </remarks>
    public static ParameterCountOptions Read(AnalyzerConfigOptions options) => new(
        ReadPositiveInt(options, MaximumRuleKey, MaximumGeneralKey, DefaultMaximum),
        ReadBool(options, CheckRecordsRuleKey, CheckRecordsGeneralKey, fallback: false),
        ReadBool(options, CountOptionalRuleKey, CountOptionalGeneralKey, fallback: true));

    /// <summary>Reads a positive integer setting, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="fallback">The value used when neither key parses.</param>
    /// <returns>The configured positive integer, or <paramref name="fallback"/>.</returns>
    private static int ReadPositiveInt(AnalyzerConfigOptions options, string ruleKey, string generalKey, int fallback)
    {
        if (options.TryGetValue(ruleKey, out var value) && int.TryParse(value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return options.TryGetValue(generalKey, out value) && int.TryParse(value, out parsed) && parsed > 0
            ? parsed
            : fallback;
    }

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
