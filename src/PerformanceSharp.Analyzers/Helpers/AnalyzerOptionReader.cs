// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reads editorconfig settings with the package's rule-specific-then-project-wide fallback,
/// shared by the option records (PSH1007, PSH1017, PSH1411). Each read prefers the rule-specific
/// key and only then the general key, matching the CA-analyzer key convention.
/// </summary>
internal static class AnalyzerOptionReader
{
    /// <summary>Reads a comma-separated list, trimming entries and dropping empty ones.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The parsed values, or an empty array when neither key is set.</returns>
    public static string[] ReadCommaSeparatedList(AnalyzerConfigOptions options, string ruleKey, string generalKey)
    {
        if (!options.TryGetValue(ruleKey, out var value) && !options.TryGetValue(generalKey, out value))
        {
            return [];
        }

        var parts = value.Split(',');
        var parsed = new string[parts.Length];
        var count = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var trimmed = parts[i].Trim();
            if (trimmed.Length > 0)
            {
                parsed[count++] = trimmed;
            }
        }

        if (count == parts.Length)
        {
            return parsed;
        }

        var result = new string[count];
        Array.Copy(parsed, result, count);
        return result;
    }

    /// <summary>Reads a boolean setting that defaults to false, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The configured value, or <see langword="false"/>.</returns>
    public static bool ReadBool(AnalyzerConfigOptions options, string ruleKey, string generalKey)
    {
        if (options.TryGetValue(ruleKey, out var value) && bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return options.TryGetValue(generalKey, out value) && bool.TryParse(value, out parsed) && parsed;
    }
}
