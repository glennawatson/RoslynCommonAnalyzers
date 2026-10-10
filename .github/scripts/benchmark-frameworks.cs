using System.Diagnostics;
using System.Text.Json;

internal static class BenchmarkFrameworks
{
    internal static string[] Projects(string suite) => suite switch
    {
        "stylesharp" => ["benchmarks/StyleSharp.Analyzers.Benchmarks"],
        "performancesharp" => ["benchmarks/PerformanceSharp.Analyzers.Benchmarks"],
        "securitysharp" => ["benchmarks/SecuritySharp.Analyzers.Benchmarks"],
        "all" => ["benchmarks/StyleSharp.Analyzers.Benchmarks", "benchmarks/PerformanceSharp.Analyzers.Benchmarks", "benchmarks/SecuritySharp.Analyzers.Benchmarks"],
        _ => throw new ArgumentException($"Unknown benchmark suite '{suite}'.", nameof(suite)),
    };

    // Compare both revisions under the same runtime. Old revisions may only target .NET 10.
    internal static async Task<string> SelectAsync(IEnumerable<string> projects)
    {
        HashSet<string> common = ["net11.0", "net10.0"];
        foreach (var project in projects)
        {
            var file = Directory.Exists(project) ? Directory.GetFiles(project, "*.csproj").Single() : project;
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in (string[])["msbuild", file, "-getProperty:TargetFramework,TargetFrameworks"])
            {
                start.ArgumentList.Add(argument);
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Cannot read benchmark frameworks for {file}: {await error}");
            }

            using var document = JsonDocument.Parse(await output);
            var properties = document.RootElement.GetProperty("Properties");
            var frameworks = string.Concat(properties.GetProperty("TargetFramework").GetString(), ";", properties.GetProperty("TargetFrameworks").GetString());
            common.IntersectWith(frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return common.Contains("net11.0") ? "net11.0" : common.Contains("net10.0") ? "net10.0"
            : throw new InvalidOperationException("The benchmark projects have no common .NET 10 or .NET 11 target.");
    }
}
