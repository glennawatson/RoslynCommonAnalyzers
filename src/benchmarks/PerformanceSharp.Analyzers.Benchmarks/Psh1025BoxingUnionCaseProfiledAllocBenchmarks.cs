// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if ROSLYN_5_9_OR_GREATER

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile (EventPipe) benchmarks for the boxing-union-case analyzer.</summary>
[System.Diagnostics.DebuggerDisplay("Psh1025BoxingUnionCaseProfiledAllocBenchmarks: {Nodes}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class Psh1025BoxingUnionCaseProfiledAllocBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = Psh1025BoxingUnionCaseBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks the clean boxing-union-case path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> Psh1025BoxingUnionCase_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating boxing-union-case path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> Psh1025BoxingUnionCase_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}

#endif
