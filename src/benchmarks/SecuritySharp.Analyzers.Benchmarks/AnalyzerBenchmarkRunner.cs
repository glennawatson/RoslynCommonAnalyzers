// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Runs one analyzer over a prepared benchmark scenario.</summary>
internal static class AnalyzerBenchmarkRunner
{
    /// <summary>Executes the analyzers and returns the diagnostic count.</summary>
    /// <param name="scenario">The benchmark scenario.</param>
    /// <param name="analyzers">The analyzers to run.</param>
    /// <returns>The number of produced diagnostics.</returns>
    public static Task<int> GetDiagnosticCountAsync(in AnalyzerBenchmarkScenario scenario, ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        if (scenario.OptionsProvider is null)
        {
            return GetDiagnosticCountAsync(scenario.Compilation.WithAnalyzers(analyzers));
        }

        var options = new CompilationWithAnalyzersOptions(
            new([], scenario.OptionsProvider),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);

        return GetDiagnosticCountAsync(scenario.Compilation.WithAnalyzers(analyzers, options));
    }

    /// <summary>Executes one prepared analyzer driver and returns the diagnostic count.</summary>
    /// <param name="withAnalyzers">The analyzer driver to execute.</param>
    /// <returns>The number of produced diagnostics.</returns>
    private static async Task<int> GetDiagnosticCountAsync(CompilationWithAnalyzers withAnalyzers)
        => (await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false)).Length;
}
