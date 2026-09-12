// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for SST2338 prefer-union analysis.</summary>
[System.Diagnostics.DebuggerDisplay("Sst2338PreferUnionBenchmarks: {Nodes}")]
[MemoryDiagnoser]
[ShortRunJob]
public class Sst2338PreferUnionBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = Sst2338PreferUnionBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks the clean prefer-union path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> Sst2338PreferUnion_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating prefer-union path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> Sst2338PreferUnion_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
