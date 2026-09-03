// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Static helpers shared by per-analyzer benchmark suites.</summary>
internal static class SingleAnalyzerBenchmarkHelper
{
    /// <summary>The do-nothing analyzer set used to measure the harness floor.</summary>
    private static readonly ImmutableArray<DiagnosticAnalyzer> BaselineAnalyzers = [new HarnessBaselineAnalyzer()];

    /// <summary>Creates one analyzer state from prepared clean and violating scenarios.</summary>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="cleanScenario">The clean benchmark scenario.</param>
    /// <param name="violatingScenario">The violating benchmark scenario.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(
        DiagnosticAnalyzer analyzer,
        AnalyzerBenchmarkScenario cleanScenario,
        AnalyzerBenchmarkScenario violatingScenario)
        => new([analyzer], cleanScenario, violatingScenario);

    /// <summary>Runs the clean benchmark scenario.</summary>
    /// <param name="state">The prepared benchmark state.</param>
    /// <returns>The number of diagnostics produced.</returns>
    public static Task<int> RunCleanAsync(SingleAnalyzerBenchmarkState state)
        => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(state.CleanScenario, state.Analyzers);

    /// <summary>Runs the violating benchmark scenario.</summary>
    /// <param name="state">The prepared benchmark state.</param>
    /// <returns>The number of diagnostics produced.</returns>
    public static Task<int> RunViolatingAsync(SingleAnalyzerBenchmarkState state)
        => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(state.ViolatingScenario, state.Analyzers);

    /// <summary>Runs the violating scenario with an analyzer that reports nothing.</summary>
    /// <param name="state">The prepared benchmark state.</param>
    /// <returns>The number of diagnostics produced, always zero.</returns>
    /// <remarks>
    /// Subtract this from the violating result to get the rule's own cost. What remains is the driver
    /// and the compiler doing the work any semantic rule causes before its logic runs.
    /// </remarks>
    public static Task<int> RunCompilerBaselineAsync(SingleAnalyzerBenchmarkState state)
        => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(state.ViolatingScenario, BaselineAnalyzers);
}
