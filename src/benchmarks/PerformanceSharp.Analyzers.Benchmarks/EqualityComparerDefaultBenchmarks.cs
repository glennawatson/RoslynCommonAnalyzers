// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for type parameter equality analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class EqualityComparerDefaultBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = EqualityComparerDefaultBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks the clean type parameter equality path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> EqualityComparerDefault_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating type parameter equality path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> EqualityComparerDefault_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
