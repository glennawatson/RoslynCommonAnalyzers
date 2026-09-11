// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for override-parameter-contract analysis (SST2424, SST2426).</summary>
[System.Diagnostics.DebuggerDisplay("OverrideParameterContractBenchmarks: {Nodes}")]
[MemoryDiagnoser]
[ShortRunJob]
public class OverrideParameterContractBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = SingleAnalyzerBenchmarkCases.Create(new OverrideParameterContractAnalyzer(), OverrideParameterContractBenchmarkSource.Generate, Nodes);

    /// <summary>Benchmarks the clean override-parameter-contract path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> OverrideParameterContract_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating override-parameter-contract path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> OverrideParameterContract_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
