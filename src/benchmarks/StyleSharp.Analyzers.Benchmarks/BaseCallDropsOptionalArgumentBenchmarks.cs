// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the base-call-drops-optional-argument analysis (SST2425).</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class BaseCallDropsOptionalArgumentBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = SingleAnalyzerBenchmarkCases.Create(new Sst2425BaseCallDropsOptionalArgumentAnalyzer(), BaseCallDropsOptionalArgumentBenchmarkSource.Generate, Nodes);

    /// <summary>Benchmarks the clean base-call-drops-optional-argument path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> BaseCallDropsOptionalArgument_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating base-call-drops-optional-argument path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> BaseCallDropsOptionalArgument_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
