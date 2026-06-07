// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile benchmarks for use-nullable-shorthand analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class UseNullableShorthandProfiledAllocBenchmarks
{
    private SingleAnalyzerBenchmarkState _state = null!;

    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    [GlobalSetup]
    public void Setup() => _state = SemanticTypeBenchmarkCases.CreateUseNullableShorthand(Nodes);

    [Benchmark]
    public Task<int> UseNullableShorthand_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    [Benchmark]
    public Task<int> UseNullableShorthand_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
