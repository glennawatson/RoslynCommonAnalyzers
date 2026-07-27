// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Allocation-profile benchmarks for anonymous-type-to-tuple analysis (PSH1023).</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class PreferTupleOverAnonymousTypeProfiledAllocBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = SingleAnalyzerBenchmarkHelper.Create(
        new Psh1023PreferTupleOverAnonymousTypeAnalyzer(),
        new(BenchmarkCompilationFactory.CreateCompilation(PreferTupleOverAnonymousTypeBenchmarkSource.Generate(Nodes, violating: false)).Compilation),
        new(BenchmarkCompilationFactory.CreateCompilation(PreferTupleOverAnonymousTypeBenchmarkSource.Generate(Nodes, violating: true)).Compilation));

    /// <summary>Benchmarks the path where the local pair is already a tuple.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> PreferTupleOverAnonymousType_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the path where every local pair is an anonymous type.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> PreferTupleOverAnonymousType_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
