// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Static helpers for building clean and violating benchmark state from one source generator.</summary>
internal static class SingleAnalyzerBenchmarkCases
{
    /// <summary>Creates one analyzer state from a clean/violating source generator pair.</summary>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="sourceFactory">Builds clean or violating source for the requested node count.</param>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(
        DiagnosticAnalyzer analyzer,
        Func<int, bool, string> sourceFactory,
        int nodes)
        => Create(analyzer, sourceFactory, nodes, []);

    /// <summary>Creates one analyzer state, forcing the supplied opt-in rule ids on so the analyzer runs.</summary>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="sourceFactory">Builds clean or violating source for the requested node count.</param>
    /// <param name="nodes">The synthetic node count.</param>
    /// <param name="enabledRuleIds">Diagnostic ids to force on for disabled-by-default (opt-in) rules.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(
        DiagnosticAnalyzer analyzer,
        Func<int, bool, string> sourceFactory,
        int nodes,
        IReadOnlyList<string> enabledRuleIds)
        => SingleAnalyzerBenchmarkHelper.Create(
            analyzer,
            CreateScenario(sourceFactory, nodes, violating: false, enabledRuleIds),
            CreateScenario(sourceFactory, nodes, violating: true, enabledRuleIds));

    /// <summary>Builds one clean or violating scenario from the provided source generator.</summary>
    /// <param name="sourceFactory">Builds clean or violating source for the requested node count.</param>
    /// <param name="nodes">The synthetic node count.</param>
    /// <param name="violating">Whether to build the violating scenario.</param>
    /// <param name="enabledRuleIds">Diagnostic ids to force on for disabled-by-default (opt-in) rules.</param>
    /// <returns>The prepared benchmark scenario.</returns>
    private static AnalyzerBenchmarkScenario CreateScenario(
        Func<int, bool, string> sourceFactory,
        int nodes,
        bool violating,
        IReadOnlyList<string> enabledRuleIds)
        => new(BenchmarkCompilationFactory.CreateCompilation(sourceFactory(nodes, violating), enabledRuleIds).Compilation);
}
