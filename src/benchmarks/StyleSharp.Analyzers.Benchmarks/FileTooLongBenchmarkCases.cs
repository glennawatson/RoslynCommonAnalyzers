// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for file-length analysis.</summary>
internal static class FileTooLongBenchmarkCases
{
    /// <summary>The rule's maximum-file-lines key.</summary>
    private const string MaximumKey = "stylesharp.SST1522.max_file_lines";

    /// <summary>The maximum used by the clean scenario, which no corpus reaches.</summary>
    private const string CleanMaximum = "1000000";

    /// <summary>The maximum used by the violating scenario, which every corpus exceeds.</summary>
    private const string ViolatingMaximum = "5";

    /// <summary>The options that keep the corpus inside the limit.</summary>
    private static readonly BenchmarkAnalyzerConfigOptionsProvider CleanOptions = CreateOptions(CleanMaximum);

    /// <summary>The options that put the corpus over the limit.</summary>
    private static readonly BenchmarkAnalyzerConfigOptionsProvider ViolatingOptions = CreateOptions(ViolatingMaximum);

    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
    {
        var source = FileTooLongBenchmarkSource.Generate(nodes);
        return SingleAnalyzerBenchmarkHelper.Create(
            new Sst1522FileTooLongAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(source).Compilation, CleanOptions),
            new(BenchmarkCompilationFactory.CreateCompilation(source).Compilation, ViolatingOptions));
    }

    /// <summary>Builds an options provider that sets the rule's maximum for every tree.</summary>
    /// <param name="maximum">The maximum to configure.</param>
    /// <returns>The options provider.</returns>
    private static BenchmarkAnalyzerConfigOptionsProvider CreateOptions(string maximum)
        => new(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal) { [MaximumKey] = maximum });
}
