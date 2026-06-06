// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared setup for extension-block analyzer microbenchmarks.</summary>
public abstract class ExtensionBlockBenchmarkBase : SingleAnalyzerBenchmarkBase
{
    /// <summary>Benchmarks the clean extension-block path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int ExtensionBlock_Clean() => RunClean();

    /// <summary>Benchmarks the violating extension-block path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int ExtensionBlock_Violating() => RunViolating();

    /// <inheritdoc/>
    protected override DiagnosticAnalyzer CreateAnalyzer() => new ExtensionBlockAnalyzer();

    /// <inheritdoc/>
    protected override AnalyzerBenchmarkScenario CreateScenario(bool violating, int nodes)
        => new(BenchmarkCompilationFactory.CreateCompilation(ExtensionBlockBenchmarkSource.Generate(nodes, violating)).Compilation);
}

/// <summary>Memory benchmarks for extension-block analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ExtensionBlockBenchmarks : ExtensionBlockBenchmarkBase;

/// <summary>Allocation-profile benchmarks for extension-block analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class ExtensionBlockProfiledAllocBenchmarks : ExtensionBlockBenchmarkBase;

/// <summary>CPU-profile benchmarks for extension-block analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class ExtensionBlockProfiledCpuBenchmarks : ExtensionBlockBenchmarkBase;
