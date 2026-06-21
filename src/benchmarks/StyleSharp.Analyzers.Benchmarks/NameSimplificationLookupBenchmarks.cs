// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Benchmarks name-simplification paths split by lookup certainty.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class NameSimplificationLookupBenchmarks
{
    /// <summary>The analyzer under benchmark.</summary>
    private readonly ImmutableArray<DiagnosticAnalyzer> _analyzers = [new NameSimplificationAnalyzer()];

    /// <summary>The source where lookup should prove the shorter spelling.</summary>
    private AnalyzerBenchmarkScenario _unshadowedLookupScenario;

    /// <summary>The source where lookup should reject shadowed shorter spellings.</summary>
    private AnalyzerBenchmarkScenario _shadowedLookupScenario;

    /// <summary>The source where generic names still need speculative binding.</summary>
    private AnalyzerBenchmarkScenario _genericFallbackScenario;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the isolated benchmark scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _unshadowedLookupScenario = new(BenchmarkCompilationFactory.CreateCompilation(
            NameSimplificationBenchmarkSource.GenerateUnshadowedLookup(Nodes)).Compilation);
        _shadowedLookupScenario = new(BenchmarkCompilationFactory.CreateCompilation(
            NameSimplificationBenchmarkSource.GenerateShadowedLookup(Nodes)).Compilation);
        _genericFallbackScenario = new(BenchmarkCompilationFactory.CreateCompilation(
            NameSimplificationBenchmarkSource.GenerateGenericFallback(Nodes)).Compilation);
    }

    /// <summary>Benchmarks unshadowed names that can be decided by symbol lookup.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> NameSimplification_UnshadowedLookup()
        => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(_unshadowedLookupScenario, _analyzers);

    /// <summary>Benchmarks shadowed names that lookup should reject without speculative binding.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> NameSimplification_ShadowedLookup()
        => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(_shadowedLookupScenario, _analyzers);

    /// <summary>Benchmarks generic names that fall back to speculative binding.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> NameSimplification_GenericFallback()
        => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(_genericFallbackScenario, _analyzers);
}
