// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile (EventPipe) benchmarks for the redundant-conversion rule.</summary>
[System.Diagnostics.DebuggerDisplay("RedundantCastProfiledAllocBenchmarks: {Nodes}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class RedundantCastProfiledAllocBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = RedundantCastBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks conversions the rule has to look at and reject.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> RedundantCast_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks conversions the rule reports.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> RedundantCast_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
