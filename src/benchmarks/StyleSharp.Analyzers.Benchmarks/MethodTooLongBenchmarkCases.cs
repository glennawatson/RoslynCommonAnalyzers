// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for member-length analysis.</summary>
internal static class MethodTooLongBenchmarkCases
{
    /// <summary>The maximum both scenarios are measured against.</summary>
    private static readonly BenchmarkAnalyzerConfigOptionsProvider OptionsProvider = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal) { ["stylesharp.SST1523.max_member_lines"] = "5" });

    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new Sst1523MethodTooLongAnalyzer(),
            new(
                BenchmarkCompilationFactory.CreateCompilation(MethodTooLongBenchmarkSource.Generate(nodes, violating: false)).Compilation,
                OptionsProvider),
            new(
                BenchmarkCompilationFactory.CreateCompilation(MethodTooLongBenchmarkSource.Generate(nodes, violating: true)).Compilation,
                OptionsProvider));
}
