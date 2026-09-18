// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Measures collection caching and loop constant analysis.</summary>
[System.Diagnostics.DebuggerDisplay("RuleInteractionBenchmarks: {Rule}, {Nodes}")]
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class RuleInteractionBenchmarks
{
    /// <summary>The prepared application corpora.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>The startup corpus.</summary>
    private AnalyzerBenchmarkScenario _startup;

    /// <summary>Gets or sets the rule whose interaction is measured.</summary>
    [Params("PSH1402", "PSH1017")]
    public string Rule { get; set; } = "PSH1402";

    /// <summary>Gets or sets the application corpus size.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds and validates every corpus before measurement.</summary>
    /// <returns>A task representing preparation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _state = RuleInteractionBenchmarkCases.Create(Rule, Nodes);
        _startup = new(BenchmarkCompilationFactory.CreateCompilation("class Startup { }").Compilation);
        await RuleInteractionBenchmarkCases.ValidateAsync(_state, _startup, Nodes).ConfigureAwait(false);
    }

    /// <summary>Measures registration on one empty class.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Startup() => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(_startup, _state.Analyzers);

    /// <summary>Measures advancing loop counters and initialized collection properties.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Measures unchanged loop operands and collection copies on each property read.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
