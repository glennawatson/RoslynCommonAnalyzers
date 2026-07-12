// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for sorted-set extreme-property analysis.</summary>
internal static class UseSortedSetExtremePropertyBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new Psh1122UseSortedSetExtremePropertyAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(UseSortedSetExtremePropertyBenchmarkSource.Generate(nodes, violating: false)).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(UseSortedSetExtremePropertyBenchmarkSource.Generate(nodes, violating: true)).Compilation));
}
