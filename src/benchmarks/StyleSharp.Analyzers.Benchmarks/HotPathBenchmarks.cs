// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory/allocation benchmarks for the hottest analyzer pipelines.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class HotPathBenchmarks : HotPathBenchmarkBase
{
    /// <inheritdoc cref="HotPathBenchmarkBase.RunLineScanClean"/>
    [Benchmark(Baseline = true)]
    public int LineScan_Clean() => RunLineScanClean();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunLineScanViolating"/>
    [Benchmark]
    public int LineScan_Violating() => RunLineScanViolating();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunTupleClean"/>
    [Benchmark]
    public int TupleElementName_Clean() => RunTupleClean();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunTupleViolating"/>
    [Benchmark]
    public int TupleElementName_Violating() => RunTupleViolating();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunTupleAnalyzerCleanAsync"/>
    [Benchmark]
    public Task<int> TupleElementNameAnalyzer_Clean() => RunTupleAnalyzerCleanAsync();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunTupleAnalyzerViolatingAsync"/>
    [Benchmark]
    public Task<int> TupleElementNameAnalyzer_Violating() => RunTupleAnalyzerViolatingAsync();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunUseNameofClean"/>
    [Benchmark]
    public int UseNameof_Clean() => RunUseNameofClean();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunUseNameofViolating"/>
    [Benchmark]
    public int UseNameof_Violating() => RunUseNameofViolating();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunArgumentGuardClean"/>
    [Benchmark]
    public int ArgumentGuard_Clean() => RunArgumentGuardClean();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunArgumentGuardViolating"/>
    [Benchmark]
    public int ArgumentGuard_Violating() => RunArgumentGuardViolating();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunArgumentGuardAnalyzerCleanAsync"/>
    [Benchmark]
    public Task<int> ArgumentGuardAnalyzer_Clean() => RunArgumentGuardAnalyzerCleanAsync();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunArgumentGuardAnalyzerViolatingAsync"/>
    [Benchmark]
    public Task<int> ArgumentGuardAnalyzer_Violating() => RunArgumentGuardAnalyzerViolatingAsync();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunSpacingCleanAsync"/>
    [Benchmark]
    public Task<int> Spacing_Clean() => RunSpacingCleanAsync();

    /// <inheritdoc cref="HotPathBenchmarkBase.RunSpacingViolatingAsync"/>
    [Benchmark]
    public Task<int> Spacing_Violating() => RunSpacingViolatingAsync();
}
