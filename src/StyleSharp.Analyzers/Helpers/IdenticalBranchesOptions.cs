// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1476 settings for one syntax tree.</summary>
/// <param name="MinimumStatements">The smallest branch body, in statements, that counts as a duplicate.</param>
internal readonly record struct IdenticalBranchesOptions(int MinimumStatements)
{
    /// <summary>The default smallest body size, which counts even a single statement.</summary>
    public const int DefaultMinimumStatements = 1;

    /// <summary>The rule-specific minimum-body key.</summary>
    private const string MinimumStatementsRuleKey = "stylesharp.SST1476.minimum_statements";

    /// <summary>The project-wide minimum-body key.</summary>
    private const string MinimumStatementsGeneralKey = "stylesharp.minimum_statements";

    /// <summary>Reads the settings for one tree, falling back to the default.</summary>
    /// <param name="options">The analyzer config options for the construct's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// An unset or unparsable value yields the default rather than an extreme, so a typo neither silences the
    /// rule nor changes which constructs it reports. A body of an expression — a conditional expression's arm,
    /// a switch expression's arm — counts as one statement, so raising the minimum above one excludes those
    /// shapes along with the one-line <c>if</c>.
    /// </remarks>
    public static IdenticalBranchesOptions Read(AnalyzerConfigOptions options)
        => new(ReadPositiveInt(options, MinimumStatementsRuleKey, MinimumStatementsGeneralKey, DefaultMinimumStatements));

    /// <summary>Reads a positive integer setting, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="fallback">The value used when neither key parses.</param>
    /// <returns>The configured positive integer, or <paramref name="fallback"/>.</returns>
    private static int ReadPositiveInt(AnalyzerConfigOptions options, string ruleKey, string generalKey, int fallback)
    {
        if (TryReadPositiveInt(options, ruleKey, out var parsed))
        {
            return parsed;
        }

        return TryReadPositiveInt(options, generalKey, out parsed) ? parsed : fallback;
    }

    /// <summary>Reads one positive integer key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="key">The key to read.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns><see langword="true"/> when the key is set to a positive integer.</returns>
    private static bool TryReadPositiveInt(AnalyzerConfigOptions options, string key, out int value)
    {
        if (options.TryGetValue(key, out var text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value > 0)
        {
            return true;
        }

        value = 0;
        return false;
    }
}
