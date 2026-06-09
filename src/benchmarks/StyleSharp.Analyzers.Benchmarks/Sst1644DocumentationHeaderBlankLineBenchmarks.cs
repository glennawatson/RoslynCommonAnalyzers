// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for documentation-header blank-line analysis (SST1644).</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class Sst1644DocumentationHeaderBlankLineBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = Sst1644DocumentationHeaderBlankLineBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks the clean documentation-header blank-line path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> Sst1644DocumentationHeaderBlankLine_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating documentation-header blank-line path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> Sst1644DocumentationHeaderBlankLine_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
