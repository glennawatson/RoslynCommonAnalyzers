// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile benchmarks for SearchValues analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class UseSearchValuesProfiledAllocBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = UseSearchValuesBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks the clean SearchValues path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> UseSearchValues_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating SearchValues path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> UseSearchValues_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
