#!/usr/bin/env dotnet
#:property TargetFramework=net11.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BenchmarkReport;
using static System.Environment;

if (args is not [var results, var reportPath, var baseline, var candidate, var suite, var filter])
{
    Console.WriteLine("::error::Expected the results folder, report path, baseline, candidate, suite and filter arguments.");
    return 2;
}

using var plan = JsonDocument.Parse(GetEnvironmentVariable("SLICES") is { Length: > 0 } planned ? planned : "[]");
var (rows, slices) = LoadRows(results, plan);

if (rows.Count == 0)
{
    Console.WriteLine("::error::No benchmark results were exported. Check the slice jobs.");
    return 1;
}

var counts = rows.CountBy(row => row.Result).ToDictionary(StringComparer.Ordinal);
var overview = new StringBuilder()
    .AppendLine("# A/B benchmark results")
    .AppendLine()
    .AppendLine($"Baseline `{baseline}`, candidate `{candidate}`, suite `{suite}`, filter `{filter}`.")
    .AppendLine()
    .AppendLine("Each slice runs base, head, head, base on its own runner, so base and head always share hardware. A benchmark is faster or slower only when the 99.9% confidence intervals of its means separate in both orders.")
    .AppendLine()
    .AppendLine("| Result | Benchmarks |")
    .AppendLine("|---|---:|");
foreach (var result in (string[])["slower", "faster", "inconclusive", "incomplete", "base only", "head only"])
{
    overview.AppendLine($"| {result} | {counts.GetValueOrDefault(result)} |");
}

overview.AppendLine();

var slower = Section("Slower", rows.Where(static row => row.Result == "slower").OrderByDescending(static row => Math.Min(row.FirstRatio!.Value, row.SecondRatio!.Value)));
var faster = Section("Faster", rows.Where(static row => row.Result == "faster").OrderBy(static row => Math.Max(row.FirstRatio!.Value, row.SecondRatio!.Value)));

var sliceTable = new StringBuilder()
    .AppendLine("## Slices")
    .AppendLine()
    .AppendLine("| Slice | Project | Benchmarks | Passes exported |")
    .AppendLine("|---|---|---:|---|");
foreach (var slice in slices)
{
    sliceTable.AppendLine($"| {slice.Slice} | {slice.Project} | {slice.Benchmarks} | {slice.Passes} of 4{(slice.Passes < 4 ? " (timed out or failed)" : string.Empty)} |");
}

sliceTable.AppendLine();

var all = new StringBuilder().AppendLine("## All results").AppendLine();
foreach (var project in rows.GroupBy(static row => row.Project).OrderBy(static group => group.Key, StringComparer.Ordinal))
{
    all.AppendLine($"<details><summary>{project.Key} ({project.Count()} benchmarks)</summary>")
        .AppendLine()
        .AppendLine(TableHeader);
    foreach (var row in project.OrderBy(static row => row.Key, StringComparer.Ordinal))
    {
        all.AppendLine(RowLine(row));
    }

    all.AppendLine().AppendLine("</details>").AppendLine();
}

var report = string.Concat(overview, slower, faster, sliceTable, all);
File.WriteAllText(reportPath, report);

var summary = report.Length <= SummaryBudget
    ? report
    : string.Concat(overview, slower, faster, sliceTable, "The full results are in the `benchmark-ab-report` artifact.\n");
if (summary.Length > SummaryBudget)
{
    summary = string.Concat(overview, sliceTable, "The report is too large for the run summary. Read it in the `benchmark-ab-report` artifact.\n");
}

File.AppendAllText(GetEnvironmentVariable("GITHUB_STEP_SUMMARY")!, summary);

var missing = 0;
foreach (var slice in slices.Where(static slice => slice.Passes < 4))
{
    Console.WriteLine($"::error::{slice.Slice} exported {slice.Passes} of 4 passes.");
    missing++;
}

return missing > 0 ? 1 : 0;

internal static partial class BenchmarkReport
{
    // A step summary over 1 MiB is dropped whole.
    internal const int SummaryBudget = 900 * 1024;
    internal const string TableHeader = "| Benchmark | Base | Head | Head/Base (1st) | Head/Base (2nd) | Allocated base | Allocated head | Result |\n|---|---:|---:|---:|---:|---:|---:|---|";

    internal static (List<Row> Rows, List<SliceStatus> Slices) LoadRows(string results, JsonDocument plan)
    {
        string[] passes = ["r1/base", "r1/head", "r2/head", "r2/base"];
        List<Row> rows = [];
        List<SliceStatus> slices = [];
        foreach (var entry in plan.RootElement.EnumerateArray())
        {
            var slice = entry.GetProperty("slice").GetString()!;
            var project = Path.GetFileName(entry.GetProperty("project").GetString()!);
            var benchmarks = entry.GetProperty("benchmarks").GetInt32();

            var measured = passes.Select(pass => Load(Path.Combine(results, slice, pass))).ToArray();
            var (firstBase, firstHead, secondHead, secondBase) = (measured[0], measured[1], measured[2], measured[3]);
            slices.Add(new(slice, project, benchmarks, measured.Count(pass => pass.Count > 0)));

            foreach (var key in measured.SelectMany(pass => pass.Keys).Distinct().Order(StringComparer.Ordinal))
            {
                var hasFirstBase = firstBase.TryGetValue(key, out var before);
                var hasSecondBase = secondBase.TryGetValue(key, out var beforeAgain);
                var hasFirstHead = firstHead.TryGetValue(key, out var after);
                var hasSecondHead = secondHead.TryGetValue(key, out var afterAgain);

                if (hasFirstBase && hasSecondBase && hasFirstHead && hasSecondHead)
                {
                    // Inconclusive means this run could not tell, not that nothing changed.
                    var result = (Verdict(before, after), Verdict(beforeAgain, afterAgain)) switch
                    {
                        ("slower", "slower") => "slower",
                        ("faster", "faster") => "faster",
                        _ => "inconclusive",
                    };

                    rows.Add(new(project, key, before, after, after.Mean / before.Mean, afterAgain.Mean / beforeAgain.Mean, result));
                    continue;
                }

                var hasBase = hasFirstBase || hasSecondBase;
                var hasHead = hasFirstHead || hasSecondHead;
                rows.Add(new(
                    project,
                    key,
                    hasFirstBase ? before : hasSecondBase ? beforeAgain : null,
                    hasFirstHead ? after : hasSecondHead ? afterAgain : null,
                    null,
                    null,
                    (hasBase, hasHead) switch
                    {
                        (true, true) => "incomplete",
                        (true, false) => "base only",
                        _ => "head only",
                    }));
            }
        }

        return (rows, slices);
    }

    internal static string Section(string title, IEnumerable<Row> rows)
    {
        var section = new StringBuilder().AppendLine($"## {title}").AppendLine();
        var any = false;
        foreach (var row in rows)
        {
            if (!any)
            {
                section.AppendLine(TableHeader);
                any = true;
            }

            section.AppendLine(RowLine(row));
        }

        return section.AppendLine(any ? string.Empty : "None.").AppendLine().ToString();
    }

    internal static string RowLine(Row row) =>
        $"| {row.Key.Replace("|", "\\|", StringComparison.Ordinal)} | {Format(row.Base)} | {Format(row.Head)} | {Ratio(row.FirstRatio)} | {Ratio(row.SecondRatio)} | {Bytes(row.Base)} | {Bytes(row.Head)} | {row.Result} |";

    private static string Verdict(Measurement before, Measurement after) =>
        after.Lower > before.Upper ? "slower" : after.Upper < before.Lower ? "faster" : "unresolved";

    private static string Ratio(double? ratio) => ratio is { } value ? value.ToString("0.000", CultureInfo.InvariantCulture) : "-";

    private static Dictionary<string, Measurement> Load(string folder)
    {
        var results = new Dictionary<string, Measurement>(StringComparer.Ordinal);
        if (!Directory.Exists(folder))
        {
            return results;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*-report-full*.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            foreach (var benchmark in document.RootElement.GetProperty("Benchmarks").EnumerateArray())
            {
                if (!benchmark.TryGetProperty("Statistics", out var statistics) || statistics.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var allocated = benchmark.TryGetProperty("Memory", out var memory) && memory.ValueKind == JsonValueKind.Object
                    ? memory.GetProperty("BytesAllocatedPerOperation").GetInt64()
                    : -1;

                var interval = statistics.GetProperty("ConfidenceInterval");
                results[Key(benchmark)] = new(
                    statistics.GetProperty("Mean").GetDouble(),
                    interval.GetProperty("Lower").GetDouble(),
                    interval.GetProperty("Upper").GetDouble(),
                    allocated);
            }
        }

        return results;
    }

    // Job ids differ between runs, so key on the name and job characteristics.
    private static string Key(JsonElement benchmark)
    {
        var name = benchmark.GetProperty("FullName").GetString();
        var characteristics = Characteristics().Match(benchmark.GetProperty("DisplayInfo").GetString() ?? string.Empty).Groups[1].Value;
        return characteristics is "" ? name! : $"{name} ({characteristics})";
    }

    private static string Format(Measurement? measurement)
    {
        if (measurement is not { } value)
        {
            return "-";
        }

        var (scale, unit) = value.Mean switch
        {
            >= 1_000_000_000 => (1_000_000_000d, "s"),
            >= 1_000_000 => (1_000_000d, "ms"),
            >= 1_000 => (1_000d, "us"),
            _ => (1d, "ns"),
        };

        var margin = (value.Upper - value.Mean) / scale;
        return string.Create(CultureInfo.InvariantCulture, $"{value.Mean / scale:0.00} ± {margin:0.00} {unit}");
    }

    private static string Bytes(Measurement? measurement) =>
        measurement is { Allocated: >= 0 } value ? value.Allocated.ToString("N0", CultureInfo.InvariantCulture) + " B" : "-";

    [GeneratedRegex(@"\(([^()]*)\)\s*$")]
    private static partial Regex Characteristics();
}

internal readonly record struct Measurement(double Mean, double Lower, double Upper, long Allocated);

internal sealed record Row(string Project, string Key, Measurement? Base, Measurement? Head, double? FirstRatio, double? SecondRatio, string Result);

internal readonly record struct SliceStatus(string Slice, string Project, int Benchmarks, int Passes);
