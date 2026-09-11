// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile benchmarks for modern-syntax value analysis.</summary>
[System.Diagnostics.DebuggerDisplay("ModernSyntaxValueProfiledAllocBenchmarks: {Nodes}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class ModernSyntaxValueProfiledAllocBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = ModernSyntaxValueBenchmarkCases.Create(Nodes);

    /// <summary>Benchmarks the clean modern-syntax value path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> ModernSyntaxValue_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating modern-syntax value path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> ModernSyntaxValue_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Benchmarks the same corpus with an analyzer that reports nothing.</summary>
    /// <returns>The number of diagnostics produced, always zero.</returns>
    /// <remarks>
    /// The floor to subtract: what the compiler costs to bind this corpus, before the rule under
    /// test does any work of its own.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark(Baseline = true)]
    public Task<int> ModernSyntaxValue_HarnessBaseline() => SingleAnalyzerBenchmarkHelper.RunCompilerBaselineAsync(_state);
}
