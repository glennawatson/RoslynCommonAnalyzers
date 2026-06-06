// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// End-to-end benchmark: runs every shipped analyzer over a synthetic compilation
/// via <see cref="CompilationWithAnalyzers"/>. This is the realistic "what an
/// IDE/build pays" figure and the surface used to hunt cross-analyzer bottlenecks.
/// </summary>
[MemoryDiagnoser]
public class AnalyzerThroughputBenchmarks
{
    /// <summary>The metadata references loaded from the current host runtime.</summary>
    private static readonly MetadataReference[] References = LoadReferences();

    /// <summary>The synthetic compilation used by the benchmark.</summary>
    private CSharpCompilation _compilation = null!;

    /// <summary>The full analyzer set loaded from the production assembly.</summary>
    private ImmutableArray<DiagnosticAnalyzer> _analyzers;

    /// <summary>The single analyzer used for isolated driver-overhead measurement.</summary>
    private ImmutableArray<DiagnosticAnalyzer> _singleAnalyzer;

    /// <summary>Gets or sets the number of classes in the synthetic compilation.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the compilation and instantiates all analyzers once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var tree = CSharpSyntaxTree.ParseText(AnalyzerThroughputSource.Generate(Types));
        _compilation = CSharpCompilation.Create(
            "Bench",
            [tree],
            References,
            new(OutputKind.DynamicallyLinkedLibrary));

        var assembly = typeof(Sst1154InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer).Assembly;
        _analyzers = [
            ..assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
                .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)!)
        ];

        _singleAnalyzer =
        [
            new Sst1154InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer()
        ];
    }

    /// <summary>Runs all 22 analyzers over the compilation and returns the diagnostic count.</summary>
    /// <returns>The number of analyzer diagnostics produced.</returns>
    [Benchmark]
    public Task<int> AllAnalyzers() => GetDiagnosticCountAsync(_analyzers);

    /// <summary>Runs a single analyzer, to isolate per-analyzer driver overhead.</summary>
    /// <returns>The number of analyzer diagnostics produced.</returns>
    [Benchmark]
    public Task<int> SingleAnalyzer() => GetDiagnosticCountAsync(_singleAnalyzer);

    /// <summary>Loads metadata references for the current host runtime.</summary>
    /// <returns>The runtime metadata references.</returns>
    private static MetadataReference[] LoadReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return
        [
            ..
            trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    /// <summary>Runs the configured analyzer set and returns the diagnostic count.</summary>
    /// <param name="analyzers">The analyzers to run.</param>
    /// <returns>The number of analyzer diagnostics produced.</returns>
    private async Task<int> GetDiagnosticCountAsync(ImmutableArray<DiagnosticAnalyzer> analyzers)
        => (await _compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync().ConfigureAwait(false)).Length;
}
