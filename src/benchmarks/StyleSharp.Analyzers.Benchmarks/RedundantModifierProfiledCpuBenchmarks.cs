// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>CPU-profile benchmarks for redundant-modifier analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class RedundantModifierProfiledCpuBenchmarks
{
    private SingleAnalyzerBenchmarkState _state = null!;

    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    [GlobalSetup]
    public void Setup() => _state = SemanticTypeBenchmarkCases.CreateRedundantModifier(Nodes);

    [Benchmark]
    public Task<int> RedundantModifier_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    [Benchmark]
    public Task<int> RedundantModifier_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
