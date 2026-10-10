#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:include benchmark-frameworks.cs

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using static System.Environment;

if (args is not [var suite, var filter, var perSlice])
{
    Console.WriteLine("::error::Expected the suite, filter and benchmarks-per-slice arguments.");
    return 2;
}

if (!int.TryParse(perSlice, NumberStyles.Integer, CultureInfo.InvariantCulture, out var benchmarksPerSlice) || benchmarksPerSlice < 1)
{
    Console.WriteLine($"::error::benchmarksPerSlice must be a whole number above zero, got '{perSlice}'.");
    return 2;
}

string[] filters = ["--filter", .. filter.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

var projects = BenchmarkFrameworks.Projects(suite);

var workspace = GetEnvironmentVariable("GITHUB_WORKSPACE")!;
using var timeout = new CancellationTokenSource(TimeSpan.FromHours(1));
var total = 0;
using var buffer = new MemoryStream();
using var summaryBuffer = new MemoryStream();
using (var writer = new Utf8JsonWriter(buffer))
using (var summaryWriter = new Utf8JsonWriter(summaryBuffer))
{
    writer.WriteStartArray();
    summaryWriter.WriteStartArray();
    foreach (var project in projects)
    {
        var framework = await BenchmarkFrameworks.SelectAsync((string[])[Path.Combine(workspace, "ab", "base", "src", project), Path.Combine(workspace, "ab", "head", "src", project)]);
        // Merged so a benchmark on only one side still runs.
        var benchmarks = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var side in (string[])["base", "head"])
        {
            var path = Path.Combine(workspace, "ab", side, "src", project);
            Console.WriteLine($"::group::Build {side} {Path.GetFileName(project)}");
            var build = await Process.RunAsync("dotnet", ["build", path, "-c", "Release"], cancellationToken: timeout.Token);
            Console.WriteLine("::endgroup::");
            if (build is { ExitCode: not 0 })
            {
                Console.WriteLine($"::error::Building {side} {project} exited with {build.ExitCode}");
                return build.ExitCode;
            }

            var list = await Process.RunAndCaptureTextAsync("dotnet", ["run", "--project", path, "-c", "Release", "-f", framework, "--no-build", "--", "--list", "flat", .. filters], timeout.Token);
            if (list is not { ExitStatus.ExitCode: 0, StandardOutput: var output })
            {
                Console.WriteLine($"::error::Listing {side} {project} exited with {list.ExitStatus.ExitCode}: {list.StandardError}");
                return list.ExitStatus.ExitCode;
            }

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.Contains('.') && !line.Contains(' '))
                {
                    benchmarks.Add(line);
                }
            }
        }

        // Whole classes per slice.
        List<List<string>> slices = [];
        List<string> current = [];
        foreach (var type in benchmarks.GroupBy(static name => name[..name.LastIndexOf('.')]))
        {
            var methods = type.ToArray();
            if (current.Count > 0 && current.Count + methods.Length > benchmarksPerSlice)
            {
                slices.Add(current);
                current = [];
            }

            current.AddRange(methods);
        }

        if (current.Count > 0)
        {
            slices.Add(current);
        }

        var prefix = Path.GetFileName(project).Split('.')[0];
        for (var i = 0; i < slices.Count; i++)
        {
            var name = $"{prefix}-{i + 1:00}";
            writer.WriteStartObject();
            writer.WriteString("project", project);
            writer.WriteString("slice", name);
            writer.WriteString("benchmarks", string.Join('\n', slices[i]));
            writer.WriteEndObject();

            summaryWriter.WriteStartObject();
            summaryWriter.WriteString("project", project);
            summaryWriter.WriteString("slice", name);
            summaryWriter.WriteNumber("benchmarks", slices[i].Count);
            summaryWriter.WriteEndObject();
        }

        Console.WriteLine($"{Path.GetFileName(project)}: {benchmarks.Count} benchmarks in {slices.Count} slices");
        total += benchmarks.Count;
    }

    writer.WriteEndArray();
    summaryWriter.WriteEndArray();
}

if (total == 0)
{
    Console.WriteLine("::error::The filter matched no benchmarks.");
    return 1;
}

// The benchmark names only fit in the matrix. execve rejects any single environment
// variable over 128 KB, so the compare job gets counts instead of the names.
await File.AppendAllLinesAsync(
    GetEnvironmentVariable("GITHUB_OUTPUT")!,
    [
        $"slices={Encoding.UTF8.GetString(buffer.ToArray())}",
        $"sliceSummary={Encoding.UTF8.GetString(summaryBuffer.ToArray())}",
    ]);
return 0;
