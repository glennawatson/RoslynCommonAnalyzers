// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for boolean-literal-comparison analysis.</summary>
internal static class BooleanLiteralComparisonBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new BooleanLiteralComparisonAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(BooleanLiteralComparisonBenchmarkSource.Generate(nodes, violating: false)).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(BooleanLiteralComparisonBenchmarkSource.Generate(nodes, violating: true)).Compilation));
}
