// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the configured maxima for the size-metric layout rules (SST1521–SST1524). Each rule takes a
/// rule-specific key and falls back to a project-wide key of the same name, exactly as the other
/// configurable rules do. An unset or unparsable value yields the default rather than a permissive or a
/// punitive extreme, so a typo neither disables a rule nor turns every method into a diagnostic.
/// </summary>
internal static class SizeLimitOptions
{
    /// <summary>The default SST1521 maximum line length, in characters.</summary>
    public const int DefaultMaxLineLength = 120;

    /// <summary>The default SST1522 maximum file length, in code lines.</summary>
    public const int DefaultMaxFileLines = 500;

    /// <summary>The default SST1523 maximum member length, in code lines.</summary>
    public const int DefaultMaxMemberLines = 60;

    /// <summary>The default SST1524 maximum switch-section length, in code lines.</summary>
    public const int DefaultMaxSwitchSectionLines = 20;

    /// <summary>The rule-specific SST1521 key.</summary>
    private const string MaxLineLengthRuleKey = "stylesharp.SST1521.max_line_length";

    /// <summary>The project-wide SST1521 key.</summary>
    private const string MaxLineLengthGeneralKey = "stylesharp.max_line_length";

    /// <summary>The rule-specific SST1522 key.</summary>
    private const string MaxFileLinesRuleKey = "stylesharp.SST1522.max_file_lines";

    /// <summary>The project-wide SST1522 key.</summary>
    private const string MaxFileLinesGeneralKey = "stylesharp.max_file_lines";

    /// <summary>The rule-specific SST1523 key.</summary>
    private const string MaxMemberLinesRuleKey = "stylesharp.SST1523.max_member_lines";

    /// <summary>The project-wide SST1523 key.</summary>
    private const string MaxMemberLinesGeneralKey = "stylesharp.max_member_lines";

    /// <summary>The rule-specific SST1524 key.</summary>
    private const string MaxSwitchSectionLinesRuleKey = "stylesharp.SST1524.max_switch_section_lines";

    /// <summary>The project-wide SST1524 key.</summary>
    private const string MaxSwitchSectionLinesGeneralKey = "stylesharp.max_switch_section_lines";

    /// <summary>Reads the SST1521 maximum line length for one tree.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The configured maximum, or <see cref="DefaultMaxLineLength"/>.</returns>
    public static int ReadMaxLineLength(AnalyzerConfigOptions options)
        => ReadPositiveInt(options, MaxLineLengthRuleKey, MaxLineLengthGeneralKey, DefaultMaxLineLength);

    /// <summary>Reads the SST1522 maximum file length for one tree.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The configured maximum, or <see cref="DefaultMaxFileLines"/>.</returns>
    public static int ReadMaxFileLines(AnalyzerConfigOptions options)
        => ReadPositiveInt(options, MaxFileLinesRuleKey, MaxFileLinesGeneralKey, DefaultMaxFileLines);

    /// <summary>Reads the SST1523 maximum member length for one tree.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The configured maximum, or <see cref="DefaultMaxMemberLines"/>.</returns>
    public static int ReadMaxMemberLines(AnalyzerConfigOptions options)
        => ReadPositiveInt(options, MaxMemberLinesRuleKey, MaxMemberLinesGeneralKey, DefaultMaxMemberLines);

    /// <summary>Reads the SST1524 maximum switch-section length for one tree.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The configured maximum, or <see cref="DefaultMaxSwitchSectionLines"/>.</returns>
    public static int ReadMaxSwitchSectionLines(AnalyzerConfigOptions options)
        => ReadPositiveInt(options, MaxSwitchSectionLinesRuleKey, MaxSwitchSectionLinesGeneralKey, DefaultMaxSwitchSectionLines);

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
}
