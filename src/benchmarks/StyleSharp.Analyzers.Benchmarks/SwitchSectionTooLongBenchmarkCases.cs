// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for switch-section-length analysis.</summary>
internal static class SwitchSectionTooLongBenchmarkCases
{
    /// <summary>The maximum both scenarios are measured against.</summary>
    private static readonly BenchmarkAnalyzerConfigOptionsProvider OptionsProvider = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal) { ["stylesharp.SST1524.max_switch_section_lines"] = "3" });

    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new Sst1524SwitchSectionTooLongAnalyzer(),
            new(
                BenchmarkCompilationFactory.CreateCompilation(SwitchSectionTooLongBenchmarkSource.Generate(nodes, violating: false)).Compilation,
                OptionsProvider),
            new(
                BenchmarkCompilationFactory.CreateCompilation(SwitchSectionTooLongBenchmarkSource.Generate(nodes, violating: true)).Compilation,
                OptionsProvider));
}
