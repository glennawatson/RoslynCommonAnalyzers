// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST2273 settings for one syntax tree.</summary>
/// <param name="MinWrappedStatements">The fewest statements a trailing <c>if</c> must wrap to be reported.</param>
internal readonly record struct TrailingGuardOptions(int MinWrappedStatements)
{
    /// <summary>The default minimum wrapped-statement count.</summary>
    public const int DefaultMinWrappedStatements = 2;

    /// <summary>The rule-specific minimum key.</summary>
    private const string MinRuleKey = "stylesharp.SST2273.min_wrapped_statements";

    /// <summary>The project-wide minimum key.</summary>
    private const string MinGeneralKey = "stylesharp.min_wrapped_statements";

    /// <summary>Reads the settings for one tree, falling back to the default.</summary>
    /// <param name="options">The analyzer config options for the declaration's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// A trailing one-liner such as <c>if (x) Do();</c> is left alone by default: inverting it trades no
    /// nesting for an extra jump. The threshold keeps the rule to bodies where flattening actually removes a
    /// level of indentation. An unset, non-numeric, or non-positive value keeps the default, so a typo neither
    /// disables the rule nor fires it on every single-statement <c>if</c>.
    /// </remarks>
    public static TrailingGuardOptions Read(AnalyzerConfigOptions options)
        => new(ReadPositiveInt(options, MinRuleKey, MinGeneralKey, DefaultMinWrappedStatements));

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
