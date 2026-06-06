// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared setup for documentation-text analyzer microbenchmarks.</summary>
public abstract class DocumentationTextBenchmarkBase : SingleAnalyzerBenchmarkBase
{
    /// <summary>Benchmarks the clean documentation-text path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int DocumentationText_Clean() => RunClean();

    /// <summary>Benchmarks the violating documentation-text path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int DocumentationText_Violating() => RunViolating();

    /// <inheritdoc/>
    protected override DiagnosticAnalyzer CreateAnalyzer() => new DocumentationTextAnalyzer();

    /// <inheritdoc/>
    protected override AnalyzerBenchmarkScenario CreateScenario(bool violating, int nodes)
        => new(BenchmarkCompilationFactory.CreateCompilation(DocumentationTextBenchmarkSource.Generate(nodes, violating)).Compilation);
}

/// <summary>Memory benchmarks for documentation-text analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DocumentationTextBenchmarks : DocumentationTextBenchmarkBase;

/// <summary>Allocation-profile benchmarks for documentation-text analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class DocumentationTextProfiledAllocBenchmarks : DocumentationTextBenchmarkBase;

/// <summary>CPU-profile benchmarks for documentation-text analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class DocumentationTextProfiledCpuBenchmarks : DocumentationTextBenchmarkBase;
