// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for namespace-folder analysis.</summary>
internal static class NamespaceFolderBenchmarkCases
{
    /// <summary>The analyzer-config options provider used by the namespace-folder scenarios.</summary>
    private static readonly BenchmarkAnalyzerConfigOptionsProvider OptionsProvider = new(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.ProjectDir"] = "/src/MyApp/",
            ["build_property.RootNamespace"] = "MyApp"
        });

    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new Sst1417NamespaceFolderAnalyzer(),
            new(
                BenchmarkCompilationFactory.CreateCompilation(NamespaceFolderBenchmarkSource.Generate(nodes, violating: false), NamespaceFolderBenchmarkSource.FilePath).Compilation,
                OptionsProvider),
            new(
                BenchmarkCompilationFactory.CreateCompilation(NamespaceFolderBenchmarkSource.Generate(nodes, violating: true), NamespaceFolderBenchmarkSource.FilePath).Compilation,
                OptionsProvider));
}
