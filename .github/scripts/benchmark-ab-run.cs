#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:include benchmark-frameworks.cs

using System.Diagnostics;
using static System.Environment;

if (args is not [var project, var slice, var results, var warmupCount, var iterationCount])
{
    Console.WriteLine("::error::Expected the project, slice, results folder, warmup count and iteration count arguments.");
    return 2;
}

string[] iterations = ["--warmupCount", warmupCount, "--iterationCount", iterationCount, "--launchCount", "1"];

string[] filters = ["--filter", .. GetEnvironmentVariable("BENCHMARKS")!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
var workspace = GetEnvironmentVariable("GITHUB_WORKSPACE")!;
var framework = await BenchmarkFrameworks.SelectAsync((string[])[Path.Combine(workspace, "ab", "base", "src", project), Path.Combine(workspace, "ab", "head", "src", project)]);

using var timeout = new CancellationTokenSource(TimeSpan.FromHours(6));
await RaisePriorityAsync(timeout.Token);

// Both trees build first, so no measurement shares the machine with a compiler.
foreach (var side in (string[])["base", "head"])
{
    Console.WriteLine($"::group::Build {side}");
    var build = await Process.RunAsync("dotnet", ["build", Path.Combine(workspace, "ab", side, "src", project), "-c", "Release"], cancellationToken: timeout.Token);
    Console.WriteLine("::endgroup::");
    if (build is { ExitCode: not 0 })
    {
        Console.WriteLine($"::error::Building {side} {project} exited with {build.ExitCode}");
        return build.ExitCode;
    }
}

foreach (var run in (string[][])[["r1", "base"], ["r1", "head"], ["r2", "head"], ["r2", "base"]])
{
    var (order, side) = (run[0], run[1]);

    // BenchmarkDotNet searches below the working directory, which must hold one tree.
    Directory.SetCurrentDirectory(Path.Combine(workspace, "ab", side, "src"));
    Console.WriteLine($"::group::{order} {side}");

    var status = await Process.RunAsync(
        "dotnet",
        [
            "run", "--project", Path.Combine(workspace, "ab", side, "src", project), "-c", "Release", "-f", framework, "--no-build", "--",
            .. filters, .. iterations, "--artifacts", Path.Combine(results, slice, order, side), "--exporters", "github", "fulljson",
        ], cancellationToken: timeout.Token);

    Console.WriteLine("::endgroup::");
    if (status is { ExitCode: not 0 })
    {
        Console.WriteLine($"::error::{order} {side} {slice} exited with {status.ExitCode}");
        return status.ExitCode;
    }
}

return 0;

// Processes started from here inherit the priority.
static async Task RaisePriorityAsync(CancellationToken cancellationToken)
{
    if (!OperatingSystem.IsWindows() && await Process.RunAsync("sudo", ["-n", "renice", "-n", "-20", "-p", $"{ProcessId}"], silent: true, cancellationToken: cancellationToken) is { ExitCode: 0 })
    {
        return;
    }

    Console.WriteLine("::warning::Could not raise the benchmark priority.");
}
