// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared setup for parameter-list layout analyzer microbenchmarks.</summary>
public abstract class ParameterListLayoutBenchmarkBase : SingleAnalyzerBenchmarkBase
{
    /// <summary>Benchmarks the clean parameter-list layout path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int ParameterListLayout_Clean() => RunClean();

    /// <summary>Benchmarks the violating parameter-list layout path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int ParameterListLayout_Violating() => RunViolating();

    /// <inheritdoc/>
    protected override DiagnosticAnalyzer CreateAnalyzer() => new ParameterListLayoutAnalyzer();

    /// <inheritdoc/>
    protected override AnalyzerBenchmarkScenario CreateScenario(bool violating, int nodes)
        => new(BenchmarkCompilationFactory.CreateCompilation(ParameterListLayoutBenchmarkSource.Generate(nodes, violating)).Compilation);
}

/// <summary>Memory benchmarks for parameter-list layout analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ParameterListLayoutBenchmarks : ParameterListLayoutBenchmarkBase;

/// <summary>Allocation-profile benchmarks for parameter-list layout analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class ParameterListLayoutProfiledAllocBenchmarks : ParameterListLayoutBenchmarkBase;

/// <summary>CPU-profile benchmarks for parameter-list layout analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class ParameterListLayoutProfiledCpuBenchmarks : ParameterListLayoutBenchmarkBase;
