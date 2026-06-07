// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for trivial-auto-property analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class TrivialAutoPropertyBenchmarks
{
    private SingleAnalyzerBenchmarkState _state = null!;

    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    [GlobalSetup]
    public void Setup() => _state = SemanticTypeBenchmarkCases.CreateTrivialAutoProperty(Nodes);

    [Benchmark]
    public Task<int> TrivialAutoProperty_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    [Benchmark]
    public Task<int> TrivialAutoProperty_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
