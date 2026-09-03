// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile benchmarks for reference-equality null-pattern analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class ReferenceEqualsNullPatternProfiledAllocBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = ReferenceEqualsNullPatternBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks the clean null-check path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> ReferenceEqualsNullPattern_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating null-check path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> ReferenceEqualsNullPattern_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Benchmarks the same corpus with an analyzer that reports nothing.</summary>
    /// <returns>The number of diagnostics produced, always zero.</returns>
    /// <remarks>
    /// The floor to subtract: what the compiler costs to bind this corpus, before the rule under
    /// test does any work of its own.
    /// </remarks>
    [Benchmark(Baseline = true)]
    public Task<int> ReferenceEqualsNullPattern_HarnessBaseline() => SingleAnalyzerBenchmarkHelper.RunCompilerBaselineAsync(_state);
}
