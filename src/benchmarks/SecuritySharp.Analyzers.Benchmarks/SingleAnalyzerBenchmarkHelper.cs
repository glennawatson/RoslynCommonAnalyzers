// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
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
    internal static SingleAnalyzerBenchmarkState Create(
        DiagnosticAnalyzer analyzer,
        AnalyzerBenchmarkScenario cleanScenario,
        AnalyzerBenchmarkScenario violatingScenario) =>
        new([analyzer], EnableRules(cleanScenario, analyzer), EnableRules(violatingScenario, analyzer));

    /// <summary>Runs the clean benchmark scenario.</summary>
    /// <param name="state">The prepared benchmark state.</param>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Task<int> RunCleanAsync(SingleAnalyzerBenchmarkState state) =>
        AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(state.CleanScenario, state.Analyzers);

    /// <summary>Runs the violating benchmark scenario.</summary>
    /// <param name="state">The prepared benchmark state.</param>
    /// <returns>The number of diagnostics produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Task<int> RunViolatingAsync(SingleAnalyzerBenchmarkState state) =>
        AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(state.ViolatingScenario, state.Analyzers);

    /// <summary>Enables every descriptor once during setup, including rules disabled by default.</summary>
    /// <param name="scenario">The prepared input and analyzer configuration.</param>
    /// <param name="analyzer">The analyzer whose descriptors must execute.</param>
    /// <returns>The scenario with explicit diagnostic severities.</returns>
    private static AnalyzerBenchmarkScenario EnableRules(AnalyzerBenchmarkScenario scenario, DiagnosticAnalyzer analyzer)
    {
        var compilation = scenario.Compilation;
        var descriptors = analyzer.SupportedDiagnostics;
        var overrides = new Dictionary<string, ReportDiagnostic>(
            compilation.Options.SpecificDiagnosticOptions.Count + descriptors.Length,
            StringComparer.Ordinal);
        foreach (var option in compilation.Options.SpecificDiagnosticOptions)
        {
            overrides.Add(option.Key, option.Value);
        }

        foreach (var descriptor in descriptors)
        {
            overrides[descriptor.Id] = ReportDiagnostic.Warn;
        }

        return new(
            compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(overrides)),
            scenario.OptionsProvider);
    }
}
