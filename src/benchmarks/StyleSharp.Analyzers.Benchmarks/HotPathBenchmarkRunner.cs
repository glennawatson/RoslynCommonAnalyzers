// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Runs the analyzer-reporting portions of the hot-path benchmark suites.</summary>
internal static class HotPathBenchmarkRunner
{
    /// <summary>Runs one analyzer set over one compilation and returns the diagnostic count.</summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <param name="analyzers">The analyzers to execute.</param>
    /// <returns>The number of diagnostics produced.</returns>
    public static async Task<int> GetDiagnosticCountAsync(CSharpCompilation compilation, ImmutableArray<DiagnosticAnalyzer> analyzers)
        => (await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync().ConfigureAwait(false)).Length;
}
