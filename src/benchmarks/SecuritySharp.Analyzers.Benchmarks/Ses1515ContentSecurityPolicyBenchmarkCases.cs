// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Prepares and validates the SES1515 benchmark corpora.</summary>
internal static class Ses1515ContentSecurityPolicyBenchmarkCases
{
    /// <summary>Builds the clean and violating scenarios with every analyzer descriptor enabled.</summary>
    /// <param name="nodes">The number of application types to generate.</param>
    /// <returns>The analyzer and prepared scenarios.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SingleAnalyzerBenchmarkState Create(int nodes) =>
        SingleAnalyzerBenchmarkHelper.Create(
            new Ses1515PermissiveContentSecurityPolicyAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(Ses1515ContentSecurityPolicyBenchmarkSource.Generate(nodes, violating: false)).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(Ses1515ContentSecurityPolicyBenchmarkSource.Generate(nodes, violating: true)).Compilation));

    /// <summary>Builds the empty-class startup corpus with SES1515 explicitly enabled.</summary>
    /// <returns>The startup scenario.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AnalyzerBenchmarkScenario CreateStartup() =>
        new(BenchmarkCompilationFactory.CreateCompilation("class Empty { }", ["SES1515"]).Compilation);

    /// <summary>Rejects invalid or mislabeled corpora before BenchmarkDotNet measures them.</summary>
    /// <param name="state">The analyzer and application corpora.</param>
    /// <param name="startup">The empty-class corpus.</param>
    /// <param name="nodes">The generated type count.</param>
    /// <returns>A task representing corpus validation.</returns>
    internal static async Task ValidateAsync(SingleAnalyzerBenchmarkState state, AnalyzerBenchmarkScenario startup, int nodes)
    {
        await ValidateScenarioAsync(startup, state.Analyzers, expectedCount: 0).ConfigureAwait(false);
        await ValidateScenarioAsync(state.CleanScenario, state.Analyzers, expectedCount: 0).ConfigureAwait(false);
        await ValidateScenarioAsync(
            state.ViolatingScenario,
            state.Analyzers,
            nodes * Ses1515ContentSecurityPolicyBenchmarkSource.ViolationsPerType).ConfigureAwait(false);
    }

    /// <summary>Checks compiler errors and the exact analyzer diagnostic count for one scenario.</summary>
    /// <param name="scenario">The prepared compilation.</param>
    /// <param name="analyzers">The enabled analyzer.</param>
    /// <param name="expectedCount">The required diagnostic count.</param>
    /// <returns>A task representing scenario validation.</returns>
    /// <exception cref="InvalidOperationException">The corpus does not compile or produces the wrong diagnostic count.</exception>
    private static async Task ValidateScenarioAsync(
        AnalyzerBenchmarkScenario scenario,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        int expectedCount)
    {
        foreach (var diagnostic in scenario.Compilation.GetDiagnostics(CancellationToken.None))
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                throw new InvalidOperationException(diagnostic.ToString());
            }
        }

        var actualCount = await AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(scenario, analyzers).ConfigureAwait(false);
        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException($"Expected {expectedCount} SES1515 diagnostics, found {actualCount}.");
        }
    }
}
