#!/usr/bin/env dotnet
#:property TargetFramework=net11.0

using System.Diagnostics;
using static System.Environment;

var packages = Directory.GetFiles(Path.Combine(GetEnvironmentVariable("GITHUB_WORKSPACE")!, "signed"), "*.nupkg");
if (packages is [])
{
    Console.WriteLine("::error::No signed packages were downloaded.");
    return 1;
}

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
foreach (var package in packages.Order(StringComparer.Ordinal))
{
    if (await Process.RunAsync("dotnet", ["nuget", "push", package, "--source", "https://api.nuget.org/v3/index.json", "--api-key", GetEnvironmentVariable("NUGET_API_KEY")!], cancellationToken: timeout.Token) is { ExitCode: not 0 } push)
    {
        return push.ExitCode;
    }
}

return 0;
