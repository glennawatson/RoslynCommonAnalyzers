// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for interlocked once-guard analysis.</summary>
internal static class InterlockedOnceGuardBenchmarkCases
{
    /// <summary>The opt-in diagnostics measured by this analyzer batch.</summary>
    private static readonly string[] EnabledRuleIds =
    [
        ConcurrencyRules.InterlockedOnceGuard.Id
    ];

    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new Psh1306InterlockedOnceGuardAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(InterlockedOnceGuardBenchmarkSource.Generate(nodes, violating: false), EnabledRuleIds).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(InterlockedOnceGuardBenchmarkSource.Generate(nodes, violating: true), EnabledRuleIds).Compilation));
}
