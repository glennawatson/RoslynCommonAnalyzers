// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Profiles PSH1000 startup and local-function reference paths with allocation stacks.</summary>
[System.Diagnostics.DebuggerDisplay("StaticAnonymousFunctionLocalFunctionProfiledAllocBenchmarks: {Nodes}")]
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class StaticAnonymousFunctionLocalFunctionProfiledAllocBenchmarks
{
    /// <summary>The number of lambdas per type that support the static modifier.</summary>
    private const int DiagnosticsPerType = 10;

    /// <summary>The prepared local-function scenarios.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>The empty-class compilation used to isolate startup cost.</summary>
    private AnalyzerBenchmarkScenario _startup;

    /// <summary>Gets or sets the number of types with local-function references.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds and validates every corpus before measurement.</summary>
    /// <returns>A task representing corpus validation.</returns>
    /// <exception cref="InvalidOperationException">A corpus has compiler errors or an unexpected diagnostic count.</exception>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _startup = CreateScenario("public class Empty { }");
        _state = SingleAnalyzerBenchmarkHelper.Create(
            new Psh1000StaticAnonymousFunctionAnalyzer(),
            CreateScenario(StaticAnonymousFunctionLocalFunctionBenchmarkSource.Generate(Nodes, violating: false)),
            CreateScenario(StaticAnonymousFunctionLocalFunctionBenchmarkSource.Generate(Nodes, violating: true)));

        if (await Startup().ConfigureAwait(false) != 0
            || await Clean().ConfigureAwait(false) != 0
            || await Violating().ConfigureAwait(false) != Nodes * DiagnosticsPerType)
        {
            throw new InvalidOperationException("The local-function benchmark diagnostic counts do not match the corpus contract.");
        }
    }

    /// <summary>Measures the fixed per-compilation analyzer cost.</summary>
    /// <returns>The diagnostic count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> Startup() => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(_startup, _state.Analyzers);

    /// <summary>Measures anonymous functions referencing enclosing non-static local functions.</summary>
    /// <returns>The diagnostic count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Measures anonymous functions whose local-function references permit static.</summary>
    /// <returns>The diagnostic count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Creates a scenario whose source has no compiler errors.</summary>
    /// <param name="source">The source to compile.</param>
    /// <returns>The validated scenario.</returns>
    /// <exception cref="InvalidOperationException">The source has a compiler error.</exception>
    private static AnalyzerBenchmarkScenario CreateScenario(string source)
    {
        var compilation = BenchmarkCompilationFactory.CreateCompilation(source).Compilation;
        foreach (var diagnostic in compilation.GetDiagnostics(CancellationToken.None))
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                throw new InvalidOperationException(diagnostic.ToString());
            }
        }

        return new(compilation);
    }
}
