// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Measures the pure Roslyn cost of binding the same synthetic corpus that
/// <see cref="AnalyzerThroughputBenchmarks"/> uses, with no analyzers attached.
/// This is the driver/bind floor: subtract it from the per-analyzer numbers to
/// see what each analyzer actually adds on top of full compilation binding.
/// </summary>
[MemoryDiagnoser]
public class CompilationBaselineBenchmarks
{
    /// <summary>The synthetic compilation used by the benchmark.</summary>
    private CSharpCompilation _compilation = null!;

    /// <summary>Gets or sets the number of classes in the synthetic compilation.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the compilation once per parameter set, mirroring the throughput benchmark.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var tree = CSharpSyntaxTree.ParseText(AnalyzerThroughputSource.Generate(Types));
        _compilation = CSharpCompilation.Create(
            "Bench",
            [tree],
            BenchmarkCompilationFactory.MetadataReferences,
            new(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>Fully binds the compilation with no analyzers and returns the diagnostic count.</summary>
    /// <returns>The number of compiler diagnostics produced by the full bind.</returns>
    [Benchmark]
    public int Bind() => _compilation.GetDiagnostics().Length;
}
