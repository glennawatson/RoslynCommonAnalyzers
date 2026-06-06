// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared setup for member-documentation analyzer microbenchmarks.</summary>
public abstract class MemberDocumentationBenchmarkBase : SingleAnalyzerBenchmarkBase
{
    /// <summary>Benchmarks the clean member-documentation path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int MemberDocumentation_Clean() => RunClean();

    /// <summary>Benchmarks the violating member-documentation path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int MemberDocumentation_Violating() => RunViolating();

    /// <inheritdoc/>
    protected override DiagnosticAnalyzer CreateAnalyzer() => new MemberDocumentationAnalyzer();

    /// <inheritdoc/>
    protected override AnalyzerBenchmarkScenario CreateScenario(bool violating, int nodes)
        => new(BenchmarkCompilationFactory.CreateCompilation(MemberDocumentationBenchmarkSource.Generate(nodes, violating)).Compilation);
}

/// <summary>Memory benchmarks for member-documentation analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MemberDocumentationBenchmarks : MemberDocumentationBenchmarkBase;

/// <summary>Allocation-profile benchmarks for member-documentation analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class MemberDocumentationProfiledAllocBenchmarks : MemberDocumentationBenchmarkBase;

/// <summary>CPU-profile benchmarks for member-documentation analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class MemberDocumentationProfiledCpuBenchmarks : MemberDocumentationBenchmarkBase;
