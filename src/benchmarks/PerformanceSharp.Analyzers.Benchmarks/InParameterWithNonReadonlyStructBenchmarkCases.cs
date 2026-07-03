// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for in-parameter-with-non-readonly-struct analysis.</summary>
internal static class InParameterWithNonReadonlyStructBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new Psh1003InParameterWithNonReadonlyStructAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(InParameterWithNonReadonlyStructBenchmarkSource.Generate(nodes, violating: false)).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(InParameterWithNonReadonlyStructBenchmarkSource.Generate(nodes, violating: true)).Compilation));
}
