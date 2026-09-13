// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Reads editorconfig settings with the rule-specific-then-project-wide fallback every package's
/// option records use. Each read prefers the rule-specific key (<c>&lt;package&gt;.&lt;RuleId&gt;.&lt;option&gt;</c>)
/// and only then the general key (<c>&lt;package&gt;.&lt;option&gt;</c>), matching the CA-analyzer key convention.
/// </summary>
internal static class AnalyzerOptionReader
{
    /// <summary>Reads a comma-separated list, trimming entries and dropping empty ones.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The parsed values, or an empty array when neither key is set.</returns>
    internal static string[] ReadCommaSeparatedList(AnalyzerConfigOptions options, string ruleKey, string generalKey)
    {
        if (!options.TryGetValue(ruleKey, out var value) && !options.TryGetValue(generalKey, out value))
        {
            return [];
        }

        var capacity = 1;
        foreach (var character in value)
        {
            if (character == ',')
            {
                capacity++;
            }
        }

        var parsed = new string[capacity];
        var count = 0;
        var start = 0;
        while (start < value.Length)
        {
            var end = value.IndexOf(',', start);
            if (end < 0)
            {
                end = value.Length;
            }

            var trimmed = TrimSegment(value, start, end);
            start = end + 1;
            if (trimmed.IsEmpty)
            {
                continue;
            }

            parsed[count] = trimmed.Length == value.Length ? value : trimmed.ToString();
            count++;
        }

        if (count == parsed.Length)
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
    internal static bool ReadBool(AnalyzerConfigOptions options, string ruleKey, string generalKey) =>
        options.TryGetValue(ruleKey, out var value) && bool.TryParse(value, out var parsed) ? parsed : options.TryGetValue(generalKey, out value) && bool.TryParse(value, out parsed) && parsed;

    /// <summary>Returns a segment without its leading and trailing whitespace.</summary>
    /// <param name="value">The option text.</param>
    /// <param name="start">The inclusive segment start.</param>
    /// <param name="end">The exclusive segment end.</param>
    /// <returns>The trimmed segment as a view of the option text.</returns>
    private static ReadOnlySpan<char> TrimSegment(string value, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return value.AsSpan(start, end - start);
    }
}
