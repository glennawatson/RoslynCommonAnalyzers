// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared setup for namespace-folder analyzer microbenchmarks.</summary>
public abstract class NamespaceFolderBenchmarkBase : SingleAnalyzerBenchmarkBase
{
    private static readonly BenchmarkAnalyzerConfigOptionsProvider OptionsProvider = new(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.ProjectDir"] = "/src/MyApp/",
            ["build_property.RootNamespace"] = "MyApp",
        });

    /// <summary>Benchmarks the clean namespace-folder path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int NamespaceFolder_Clean() => RunClean();

    /// <summary>Benchmarks the violating namespace-folder path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public int NamespaceFolder_Violating() => RunViolating();

    /// <inheritdoc/>
    protected override DiagnosticAnalyzer CreateAnalyzer() => new NamespaceFolderAnalyzer();

    /// <inheritdoc/>
    protected override AnalyzerBenchmarkScenario CreateScenario(bool violating, int nodes)
        => new(
            BenchmarkCompilationFactory.CreateCompilation(NamespaceFolderBenchmarkSource.Generate(nodes, violating), NamespaceFolderBenchmarkSource.FilePath).Compilation,
            OptionsProvider);
}

/// <summary>Memory benchmarks for namespace-folder analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class NamespaceFolderBenchmarks : NamespaceFolderBenchmarkBase;

/// <summary>Allocation-profile benchmarks for namespace-folder analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class NamespaceFolderProfiledAllocBenchmarks : NamespaceFolderBenchmarkBase;

/// <summary>CPU-profile benchmarks for namespace-folder analysis.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class NamespaceFolderProfiledCpuBenchmarks : NamespaceFolderBenchmarkBase;
