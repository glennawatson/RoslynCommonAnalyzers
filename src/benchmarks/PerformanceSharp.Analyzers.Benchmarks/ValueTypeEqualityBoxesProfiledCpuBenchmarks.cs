// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>CPU-profile benchmarks for value-type-equality-boxes analysis.</summary>
[System.Diagnostics.DebuggerDisplay("ValueTypeEqualityBoxesProfiledCpuBenchmarks: {Nodes}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class ValueTypeEqualityBoxesProfiledCpuBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = ValueTypeEqualityBoxesBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks the clean value-type-equality path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> ValueTypeEqualityBoxes_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating value-type-equality path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> ValueTypeEqualityBoxes_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
