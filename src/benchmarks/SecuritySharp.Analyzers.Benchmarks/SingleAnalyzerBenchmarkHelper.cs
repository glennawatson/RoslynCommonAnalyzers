// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Static helpers shared by per-analyzer benchmark suites.</summary>
internal static class SingleAnalyzerBenchmarkHelper
{
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
}
