// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>CPU-profile benchmarks for private-member usage analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class PrivateMemberUsageProfiledCpuBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic Types count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = PrivateMemberUsageBenchmarkCases.Create(Types);

    /// <summary>Benchmarks the clean path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> PrivateMemberUsage_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> PrivateMemberUsage_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Benchmarks the same corpus with an analyzer that reports nothing.</summary>
    /// <returns>The number of diagnostics produced, always zero.</returns>
    /// <remarks>
    /// The floor to subtract: what the driver and the compiler cost on this corpus, before the rule under
    /// test does any work of its own.
    /// </remarks>
    [Benchmark(Baseline = true)]
    public Task<int> PrivateMemberUsage_HarnessBaseline() => SingleAnalyzerBenchmarkHelper.RunCompilerBaselineAsync(_state);
}
