// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Prepared clean and violating scenarios for one analyzer benchmark suite.</summary>
internal sealed class SingleAnalyzerBenchmarkState
{
    /// <summary>Initializes a new instance of the <see cref="SingleAnalyzerBenchmarkState"/> class.</summary>
    /// <param name="analyzers">The analyzer set to execute.</param>
    /// <param name="cleanScenario">The clean benchmark scenario.</param>
    /// <param name="violatingScenario">The violating benchmark scenario.</param>
    public SingleAnalyzerBenchmarkState(
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        AnalyzerBenchmarkScenario cleanScenario,
        AnalyzerBenchmarkScenario violatingScenario)
    {
        Analyzers = analyzers;
        CleanScenario = cleanScenario;
        ViolatingScenario = violatingScenario;
    }

    /// <summary>Gets the analyzer set to execute.</summary>
    public ImmutableArray<DiagnosticAnalyzer> Analyzers { get; }

    /// <summary>Gets the clean benchmark scenario.</summary>
    public AnalyzerBenchmarkScenario CleanScenario { get; }

    /// <summary>Gets the violating benchmark scenario.</summary>
    public AnalyzerBenchmarkScenario ViolatingScenario { get; }
}
