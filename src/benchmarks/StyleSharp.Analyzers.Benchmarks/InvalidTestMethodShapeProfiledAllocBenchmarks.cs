// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Profiles allocations when validating NUnit test-method signatures.</summary>
[System.Diagnostics.DebuggerDisplay("InvalidTestMethodShapeProfiledAllocBenchmarks: {Scenario}, {Nodes}")]
[MemoryDiagnoser]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class InvalidTestMethodShapeProfiledAllocBenchmarks
{
    /// <summary>The prepared analyzer and compilation.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the number of test methods.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Gets or sets the selected test-method contract.</summary>
    [Params("Startup", "Ordinary", "ExpectedResult", "CaseSource", "Violating", "CombinedTest", "Theory", "Values")]
    public string Scenario { get; set; } = string.Empty;

    /// <summary>Prepares the compilation and verifies the corpus produces its expected diagnostics.</summary>
    /// <returns>A task representing corpus validation.</returns>
    /// <exception cref="InvalidOperationException">The corpus has compiler errors or does not produce the expected diagnostic count.</exception>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        var source = InvalidTestMethodShapeBenchmarkSource.Generate(Nodes, Scenario);
        var scenario = new AnalyzerBenchmarkScenario(BenchmarkCompilationFactory.CreateCompilation(source).Compilation);
        foreach (var diagnostic in scenario.Compilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                throw new InvalidOperationException(diagnostic.ToString());
            }
        }

        _state = SingleAnalyzerBenchmarkHelper.Create(new Sst2509InvalidTestMethodShapeAnalyzer(), scenario, scenario);
        var count = await SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state).ConfigureAwait(false);
        var expected = Scenario is "Violating" or "CombinedTest" or "Theory" or "Values" ? Nodes : 0;
        if (count != expected)
        {
            throw new InvalidOperationException($"Expected {expected} diagnostics for {Scenario}, received {count}.");
        }
    }

    /// <summary>Measures the selected startup, clean, or violating test-method corpus.</summary>
    /// <returns>The number of reported diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> AnalyzeAsync() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);
}
