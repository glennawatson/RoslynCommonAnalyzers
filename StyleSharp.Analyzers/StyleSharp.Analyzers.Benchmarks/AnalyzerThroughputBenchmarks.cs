// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
    private static readonly MetadataReference[] References = LoadReferences();

    private CSharpCompilation _compilation = null!;
    private ImmutableArray<DiagnosticAnalyzer> _analyzers;
    private ImmutableArray<DiagnosticAnalyzer> _singleAnalyzer;

    /// <summary>Gets or sets the number of classes in the synthetic compilation.</summary>
    [Params(50, 500)]
    public int Types { get; set; }

    /// <summary>Builds the compilation and instantiates all analyzers once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var tree = CSharpSyntaxTree.ParseText(SourceCorpus.Generate(Types));
        _compilation = CSharpCompilation.Create(
            "Bench",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var assembly = typeof(Sst0005InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer).Assembly;
        _analyzers = assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
            .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)!)
            .ToImmutableArray();

        _singleAnalyzer = ImmutableArray.Create<DiagnosticAnalyzer>(
            new Sst0005InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer());
    }

    /// <summary>Runs all 22 analyzers over the compilation and returns the diagnostic count.</summary>
    /// <returns>The number of analyzer diagnostics produced.</returns>
    [Benchmark]
    public int AllAnalyzers()
    {
        var withAnalyzers = _compilation.WithAnalyzers(_analyzers);
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;
    }

    /// <summary>Runs a single analyzer, to isolate per-analyzer driver overhead.</summary>
    /// <returns>The number of analyzer diagnostics produced.</returns>
    [Benchmark]
    public int SingleAnalyzer()
    {
        var withAnalyzers = _compilation.WithAnalyzers(_singleAnalyzer);
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;
    }

    private static MetadataReference[] LoadReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
