// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Analyzer benchmarks for private-member usage rules.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PrivateMemberUsageBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark state.</summary>
    [GlobalSetup]
    public void Setup() => _state = PrivateMemberUsageBenchmarkCases.Create(Types);

    /// <summary>Runs the analyzer over clean private-member usage.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> PrivateMemberUsage_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Runs the analyzer over unused and unread private members.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> PrivateMemberUsage_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
