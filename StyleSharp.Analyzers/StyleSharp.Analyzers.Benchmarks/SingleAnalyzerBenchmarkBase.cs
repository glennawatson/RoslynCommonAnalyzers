// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared setup for one analyzer's clean and violating compilation scenarios.</summary>
public abstract class SingleAnalyzerBenchmarkBase
{
    private AnalyzerBenchmarkScenario _cleanScenario;
    private AnalyzerBenchmarkScenario _violatingScenario;
    private ImmutableArray<DiagnosticAnalyzer> _analyzers;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(100, 1000)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _analyzers = [CreateAnalyzer()];
        _cleanScenario = CreateScenario(violating: false, Nodes);
        _violatingScenario = CreateScenario(violating: true, Nodes);
    }

    /// <summary>Creates the analyzer under test.</summary>
    /// <returns>The analyzer instance.</returns>
    protected abstract DiagnosticAnalyzer CreateAnalyzer();

    /// <summary>Creates one benchmark scenario.</summary>
    /// <param name="violating">Whether to create the violating case.</param>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared scenario.</returns>
    protected abstract AnalyzerBenchmarkScenario CreateScenario(bool violating, int nodes);

    /// <summary>Runs the clean benchmark scenario.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected int RunClean() => AnalyzerBenchmarkRunner.GetDiagnosticCount(_cleanScenario, _analyzers);

    /// <summary>Runs the violating benchmark scenario.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected int RunViolating() => AnalyzerBenchmarkRunner.GetDiagnosticCount(_violatingScenario, _analyzers);
}
