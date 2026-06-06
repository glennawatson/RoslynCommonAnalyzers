// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Runs one analyzer over a prepared benchmark scenario.</summary>
internal static class AnalyzerBenchmarkRunner
{
    /// <summary>Executes the analyzers and returns the diagnostic count.</summary>
    /// <param name="scenario">The benchmark scenario.</param>
    /// <param name="analyzers">The analyzers to run.</param>
    /// <returns>The number of produced diagnostics.</returns>
    public static int GetDiagnosticCount(in AnalyzerBenchmarkScenario scenario, ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        if (scenario.OptionsProvider is null)
        {
            return scenario.Compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;
        }

        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, scenario.OptionsProvider),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);

        return scenario.Compilation.WithAnalyzers(analyzers, options).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;
    }
}
