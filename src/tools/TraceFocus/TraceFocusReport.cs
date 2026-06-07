// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;

namespace TraceFocus;

/// <summary>Formats an aggregated trace summary for terminal output.</summary>
internal sealed class TraceFocusReport
{
    /// <summary>Shared JSON serializer options for machine-readable output.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Top analyzer-only inclusive frames shown in the shortened report.</summary>
    private readonly List<RankedEntry> _topAnalyzerFrames;

    /// <summary>Top analyzer-only leaf frames shown in the shortened report.</summary>
    private readonly List<RankedEntry> _topAnalyzerLeafFrames;

    /// <summary>Top analyzer-only stacks shown in the shortened report.</summary>
    private readonly List<RankedEntry> _topAnalyzerStacks;

    /// <summary>Top retained inclusive frames shown in the report.</summary>
    private readonly List<RankedEntry> _topFrames;

    /// <summary>Top retained leaf frames shown in the report.</summary>
    private readonly List<RankedEntry> _topLeafFrames;

    /// <summary>Top retained stacks shown in the report.</summary>
    private readonly List<RankedEntry> _topStacks;

    /// <summary>Initializes a new instance of the <see cref="TraceFocusReport"/> class.</summary>
    /// <param name="sourcePath">The source speedscope file path.</param>
    /// <param name="aggregation">The aggregated trace data.</param>
    /// <param name="filters">The active include and exclude filters.</param>
    /// <param name="options">The active command-line options.</param>
    public TraceFocusReport(string sourcePath, TraceAggregation aggregation, FrameFilters filters, Options options)
    {
        SourcePath = Path.GetRelativePath(Environment.CurrentDirectory, sourcePath);
        Unit = aggregation.Unit;
        EventedProfiles = aggregation.EventedProfiles;
        MatchedProfiles = aggregation.MatchedProfiles;
        TotalDuration = aggregation.TotalDuration;
        FocusedDuration = aggregation.FocusedDuration;
        IncludeTerms = filters.IncludeTerms;
        ExcludeTerms = filters.ExcludeTerms;
        AnalyzerOnly = options.AnalyzerOnly;
        OutputFormat = options.OutputFormat;
        ProfileKind = filters.EffectiveProfileKind;
        _topFrames = TraceFocusReportHelpers.Rank(aggregation.FrameTotals, options.TopFrames);
        _topLeafFrames = TraceFocusReportHelpers.Rank(aggregation.LeafTotals, options.TopFrames);
        _topStacks = TraceFocusReportHelpers.Rank(aggregation.StackTotals, options.TopStacks);
        _topAnalyzerFrames = TraceFocusReportHelpers.Rank(TraceFocusReportHelpers.ProjectAnalyzerEntries(aggregation.FrameTotals), options.TopFrames);
        _topAnalyzerLeafFrames = TraceFocusReportHelpers.Rank(TraceFocusReportHelpers.ProjectAnalyzerEntries(aggregation.LeafTotals), options.TopFrames);
        _topAnalyzerStacks = TraceFocusReportHelpers.Rank(TraceFocusReportHelpers.ProjectAnalyzerStacks(aggregation.StackTotals), options.TopStacks);
    }

    /// <summary>Gets a value indicating whether the shortened analyzer-only view is enabled.</summary>
    public bool AnalyzerOnly { get; }

    /// <summary>Gets the active exclude terms shown in the report.</summary>
    public List<string> ExcludeTerms { get; }

    /// <summary>Gets the number of evented profiles processed.</summary>
    public int EventedProfiles { get; }

    /// <summary>Gets the retained analyzer-visible duration.</summary>
    public double FocusedDuration { get; }

    /// <summary>Gets the active include terms shown in the report.</summary>
    public List<string> IncludeTerms { get; }

    /// <summary>Gets the number of profiles that contributed retained samples.</summary>
    public int MatchedProfiles { get; }

    /// <summary>Gets the active output format.</summary>
    public TraceOutputFormat OutputFormat { get; }

    /// <summary>Gets the effective profile kind used for default filtering.</summary>
    public TraceProfileKind ProfileKind { get; }

    /// <summary>Gets the source speedscope file path shown in the report.</summary>
    public string SourcePath { get; }

    /// <summary>Gets the total sampled duration.</summary>
    public double TotalDuration { get; }

    /// <summary>Gets the units used by the trace.</summary>
    public string Unit { get; }

    /// <summary>Writes the formatted report to the supplied text writer.</summary>
    /// <param name="writer">The destination writer.</param>
    public void WriteTo(TextWriter writer)
    {
        if (OutputFormat == TraceOutputFormat.Json)
        {
            WriteJsonTo(writer);
            return;
        }

        writer.WriteLine($"TraceFocus: {SourcePath}");
        writer.WriteLine($"Profile kind: {ProfileKind}");
        writer.WriteLine($"Unit: {Unit}");
        writer.WriteLine($"Evented profiles: {EventedProfiles}");
        writer.WriteLine($"Profiles with focused samples: {MatchedProfiles}");
        writer.WriteLine($"Total sampled duration: {FormatMetric(TotalDuration)}");
        writer.WriteLine(
            $"Focused duration: {FormatMetric(FocusedDuration)} ({TraceFocusReportHelpers.FormatPercent(FocusedDuration, TotalDuration)})");

        if (AnalyzerOnly)
        {
            writer.WriteLine();
            writer.WriteLine("Top analyzer frames:");
            WriteEntries(writer, _topAnalyzerFrames, includeStacks: false);

            writer.WriteLine();
            writer.WriteLine("Top analyzer leaf frames:");
            WriteEntries(writer, _topAnalyzerLeafFrames, includeStacks: false);

            writer.WriteLine();
            writer.WriteLine("Top analyzer stacks:");
            WriteEntries(writer, _topAnalyzerStacks, includeStacks: true);
            return;
        }

        writer.WriteLine();
        writer.WriteLine("Include filters:");
        TraceFocusReportHelpers.WriteList(writer, IncludeTerms);

        writer.WriteLine();
        writer.WriteLine("Exclude filters:");
        TraceFocusReportHelpers.WriteList(writer, ExcludeTerms);

        writer.WriteLine();
        writer.WriteLine("Top inclusive frames:");
        WriteEntries(writer, _topFrames, includeStacks: false);

        writer.WriteLine();
        writer.WriteLine("Top leaf frames:");
        WriteEntries(writer, _topLeafFrames, includeStacks: false);

        writer.WriteLine();
        writer.WriteLine("Top focused stacks:");
        WriteEntries(writer, _topStacks, includeStacks: true);
    }

    /// <summary>Formats a retained sampled value using the trace unit.</summary>
    /// <param name="value">The retained sampled value.</param>
    /// <returns>The formatted metric string.</returns>
    private string FormatMetric(double value) => value.ToString("F3", CultureInfo.InvariantCulture) + " " + Unit;

    /// <summary>Writes either frame rows or stack rows to the destination writer.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="entries">The ranked entries to print.</param>
    /// <param name="includeStacks">Whether to render multi-line stack output.</param>
    private void WriteEntries(TextWriter writer, List<RankedEntry> entries, bool includeStacks)
    {
        if (entries.Count == 0)
        {
            writer.WriteLine("  (no matching data)");
            return;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            if (includeStacks)
            {
                WriteStackEntry(writer, entries[index]);
                continue;
            }

            writer.WriteLine($"  {FormatMetric(entries[index].Value)}: {entries[index].Name}");
        }
    }

    /// <summary>Writes the current report as JSON.</summary>
    /// <param name="writer">The destination writer.</param>
    private void WriteJsonTo(TextWriter writer)
    {
        var model = new JsonReport(
            SourcePath,
            ProfileKind.ToString().ToLowerInvariant(),
            OutputFormat.ToString().ToLowerInvariant(),
            AnalyzerOnly,
            Unit,
            EventedProfiles,
            MatchedProfiles,
            TotalDuration,
            FocusedDuration,
            TotalDuration <= 0 ? 0 : FocusedDuration / TotalDuration,
            IncludeTerms,
            ExcludeTerms,
            TraceFocusReportHelpers.CreateJsonEntries<RankedEntry, JsonEntry>(
                AnalyzerOnly ? _topAnalyzerFrames : _topFrames,
                static entry => new(entry.Name, entry.Value)),
            TraceFocusReportHelpers.CreateJsonEntries<RankedEntry, JsonEntry>(
                AnalyzerOnly ? _topAnalyzerLeafFrames : _topLeafFrames,
                static entry => new(entry.Name, entry.Value)),
            TraceFocusReportHelpers.CreateJsonEntries<RankedEntry, JsonEntry>(
                AnalyzerOnly ? _topAnalyzerStacks : _topStacks,
                static entry => new(entry.Name, entry.Value)));

        writer.Write(JsonSerializer.Serialize(model, JsonOptions));
    }

    /// <summary>Writes one multi-line retained stack entry.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="entry">The retained stack entry.</param>
    private void WriteStackEntry(TextWriter writer, RankedEntry entry)
    {
        var lines = entry.Name.Split('\n');
        writer.WriteLine($"  {FormatMetric(entry.Value)}: {lines[0]}");
        for (var index = 1; index < lines.Length; index++)
        {
            writer.WriteLine($"    {lines[index]}");
        }
    }

    /// <summary>JSON row for a ranked frame or stack entry.</summary>
    /// <param name="Name">The entry name.</param>
    /// <param name="Value">The retained sampled value.</param>
    private sealed record JsonEntry(string Name, double Value);

    /// <summary>JSON report payload emitted when the tool runs in JSON mode.</summary>
    /// <param name="SourcePath">The source speedscope path.</param>
    /// <param name="ProfileKind">The effective profile kind.</param>
    /// <param name="OutputFormat">The output format.</param>
    /// <param name="AnalyzerOnly">Whether analyzer-only projection was enabled.</param>
    /// <param name="Unit">The trace unit.</param>
    /// <param name="EventedProfiles">The number of evented profiles processed.</param>
    /// <param name="MatchedProfiles">The number of profiles with focused samples.</param>
    /// <param name="TotalDuration">The total sampled duration.</param>
    /// <param name="FocusedDuration">The retained focused duration.</param>
    /// <param name="FocusedRatio">The retained focused ratio of the total duration.</param>
    /// <param name="IncludeFilters">The active include filters.</param>
    /// <param name="ExcludeFilters">The active exclude filters.</param>
    /// <param name="TopFrames">The top retained frames.</param>
    /// <param name="TopLeafFrames">The top retained leaf frames.</param>
    /// <param name="TopStacks">The top retained stacks.</param>
    private sealed record JsonReport(
        string SourcePath,
        string ProfileKind,
        string OutputFormat,
        bool AnalyzerOnly,
        string Unit,
        int EventedProfiles,
        int MatchedProfiles,
        double TotalDuration,
        double FocusedDuration,
        double FocusedRatio,
        List<string> IncludeFilters,
        List<string> ExcludeFilters,
        List<JsonEntry> TopFrames,
        List<JsonEntry> TopLeafFrames,
        List<JsonEntry> TopStacks);
}
