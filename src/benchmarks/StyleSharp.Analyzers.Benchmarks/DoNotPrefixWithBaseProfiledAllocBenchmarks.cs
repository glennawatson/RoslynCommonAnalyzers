// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile benchmarks for do-not-prefix-with-base analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class DoNotPrefixWithBaseProfiledAllocBenchmarks
{
    private SingleAnalyzerBenchmarkState _state = null!;

    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    [GlobalSetup]
    public void Setup() => _state = SemanticTypeBenchmarkCases.CreateDoNotPrefixWithBase(Nodes);

    [Benchmark]
    public Task<int> DoNotPrefixWithBase_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    [Benchmark]
    public Task<int> DoNotPrefixWithBase_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
