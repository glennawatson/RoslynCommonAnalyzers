// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

namespace PerformanceSharp.Analyzers.Benchmarks;

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

    /// <summary>Binds the violating corpus with no analyzer at all, giving the floor to subtract.</summary>
    /// <param name="state">The prepared benchmark state.</param>
    /// <returns>The number of compiler diagnostics produced.</returns>
    /// <remarks>
    /// Traces of these benchmarks are dominated by the compiler, not the rule: running any analyzer makes
    /// the compiler bind every method body, and that binding is most of what gets allocated. This measures
    /// that binding on its own, so the rule's own cost is the violating result minus this one rather than a
    /// number the compiler hides. The difference also carries the analyzer driver's own overhead, which is
    /// charged to the rule here — it is small next to binding, and counting it against the rule errs
    /// towards over-reporting cost rather than hiding it.
    /// </remarks>
    public static Task<int> RunCompilerBaselineAsync(SingleAnalyzerBenchmarkState state)
        => AnalyzerBenchmarkRunner.GetCompilerDiagnosticCountAsync(state.ViolatingScenario);
}
