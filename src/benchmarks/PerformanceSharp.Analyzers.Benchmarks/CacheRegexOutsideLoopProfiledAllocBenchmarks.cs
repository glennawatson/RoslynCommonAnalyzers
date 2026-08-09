// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile benchmarks for regex-in-loop analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class CacheRegexOutsideLoopProfiledAllocBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = SingleAnalyzerBenchmarkHelper.Create(
        new Psh1421CacheRegexOutsideLoopAnalyzer(),
        new(BenchmarkCompilationFactory.CreateCompilation(CacheRegexOutsideLoopBenchmarkSource.Generate(Nodes, violating: false)).Compilation),
        new(BenchmarkCompilationFactory.CreateCompilation(CacheRegexOutsideLoopBenchmarkSource.Generate(Nodes, violating: true)).Compilation));

    /// <summary>Benchmarks the clean path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> CacheRegexOutsideLoop_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> CacheRegexOutsideLoop_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
