// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace TraceFocus;

/// <summary>Pure helper operations for ranking and projecting trace-focus report data.</summary>
internal static class TraceFocusReportHelpers
{
    /// <summary>Arrow prefix retained on non-root lines in a formatted stack key.</summary>
    private const string StackArrowPrefix = "-> ";

    /// <summary>Converts a fraction into a percentage value.</summary>
    private const double PercentageScale = 100d;

    /// <summary>Converts ranked entries into serializable JSON rows.</summary>
    /// <typeparam name="TInput">The ranked entry type.</typeparam>
    /// <typeparam name="TOutput">The JSON row type.</typeparam>
    /// <param name="entries">The ranked entries.</param>
    /// <param name="project">The row projection callback.</param>
    /// <returns>The projected JSON rows.</returns>
    public static List<TOutput> CreateJsonEntries<TInput, TOutput>(List<TInput> entries, Func<TInput, TOutput> project)
    {
        var jsonEntries = new List<TOutput>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            jsonEntries.Add(project(entries[index]));
        }

        return jsonEntries;
    }

    /// <summary>Formats a retained value as a percentage of the total sampled duration.</summary>
    /// <param name="part">The retained value.</param>
    /// <param name="total">The total sampled value.</param>
    /// <returns>The formatted percentage string.</returns>
    public static string FormatPercent(double part, double total) =>
        total switch
        {
            <= 0 => "0.0%",
            _ => (part / total * PercentageScale).ToString("F1", CultureInfo.InvariantCulture) + "%"
        };

    /// <summary>Projects only analyzer-owned frame totals from the supplied totals table.</summary>
    /// <param name="totals">The source totals table.</param>
    /// <returns>The analyzer-only totals table.</returns>
    public static Dictionary<string, double> ProjectAnalyzerEntries(Dictionary<string, double> totals)
    {
        var projected = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var pair in totals)
        {
            if (!FrameFilters.IsAnalyzerFrame(pair.Key))
            {
                continue;
            }

            projected[pair.Key] = pair.Value;
        }

        return projected;
    }

    /// <summary>Projects and re-aggregates analyzer-only stack views from the supplied totals table.</summary>
    /// <param name="totals">The source stack totals table.</param>
    /// <returns>The analyzer-only stack totals table.</returns>
    public static Dictionary<string, double> ProjectAnalyzerStacks(Dictionary<string, double> totals)
    {
        var projected = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var pair in totals)
        {
            var analyzerStack = KeepAnalyzerStackLines(pair.Key);
            if (analyzerStack.Length == 0)
            {
                continue;
            }

            projected[analyzerStack] = projected.TryGetValue(analyzerStack, out var existing)
                ? existing + pair.Value
                : pair.Value;
        }

        return projected;
    }

    /// <summary>Ranks the largest entries from a totals table.</summary>
    /// <param name="totals">The totals table.</param>
    /// <param name="count">The number of rows to retain.</param>
    /// <returns>The ranked rows.</returns>
    public static List<RankedEntry> Rank(Dictionary<string, double> totals, int count)
    {
        var entries = totals
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Take(count);

        var rankedEntries = new List<RankedEntry>(count);
        foreach (var pair in entries)
        {
            rankedEntries.Add(new(pair.Key, pair.Value));
        }

        return rankedEntries;
    }

    /// <summary>Writes the active include or exclude filter list.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="values">The values to print.</param>
    public static void WriteList(TextWriter writer, List<string> values)
    {
        if (values.Count == 0)
        {
            writer.WriteLine("  (none)");
            return;
        }

        for (var index = 0; index < values.Count; index++)
        {
            writer.WriteLine($"  {values[index]}");
        }
    }

    /// <summary>Returns only the analyzer-owned lines from a formatted stack key.</summary>
    /// <param name="stackKey">The formatted stack key.</param>
    /// <returns>The shortened analyzer-only stack key, or an empty string.</returns>
    private static string KeepAnalyzerStackLines(string stackKey)
    {
        var lines = stackKey.Split('\n');
        var keptLines = new List<string>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith(StackArrowPrefix, StringComparison.Ordinal))
            {
                line = line[StackArrowPrefix.Length..];
            }

            if (!FrameFilters.IsAnalyzerFrame(line))
            {
                continue;
            }

            keptLines.Add(line);
        }

        return keptLines.Count == 0 ? string.Empty : string.Join("\n  -> ", keptLines);
    }
}
