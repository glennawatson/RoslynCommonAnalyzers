#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:include benchmark-frameworks.cs

if (args is not [var suite, var sourceFolder])
{
    Console.WriteLine("::error::Expected the suite and source folder arguments.");
    return 2;
}

var projects = BenchmarkFrameworks.Projects(suite);
var framework = await BenchmarkFrameworks.SelectAsync(projects.Select(project => Path.Combine(sourceFolder, project)));
var files = projects.Select(project => $"{project}/{Path.GetFileName(project)}.csproj");
await File.AppendAllLinesAsync(Environment.GetEnvironmentVariable("GITHUB_OUTPUT")!, [
    $"framework={framework}",
    "projects<<BENCHMARK_PROJECTS", .. files, "BENCHMARK_PROJECTS",
]);
Console.WriteLine($"Benchmark host: {framework}");
return 0;
