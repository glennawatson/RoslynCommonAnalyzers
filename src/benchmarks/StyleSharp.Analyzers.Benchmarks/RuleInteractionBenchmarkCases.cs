// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds and validates the interaction benchmark corpora.</summary>
internal static class RuleInteractionBenchmarkCases
{
    /// <summary>Creates the rule and its application corpora.</summary>
    /// <param name="rule">The diagnostic identifier.</param>
    /// <param name="nodes">The number of application types.</param>
    /// <returns>The prepared state.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule has no interaction corpus.</exception>
    internal static SingleAnalyzerBenchmarkState Create(string rule, int nodes)
    {
        DiagnosticAnalyzer analyzer = rule switch
        {
            "SST1436" => new EmptyCodeAnalyzer(),
            "SST1452" => new Sst1452UnusedTypeParameterAnalyzer(),
            "SST1496" => new Sst1496AbstractTypeWithoutAbstractMembersAnalyzer(),
            "SST2310" => new Sst2310ObsoleteCodeShouldBeRemovedAnalyzer(),
            "SST1420" => new Sst1420TrivialAutoPropertyAnalyzer(),
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };
        return SingleAnalyzerBenchmarkCases.Create(analyzer, (count, violating) => RuleInteractionBenchmarkSource.Generate(rule, count, violating), nodes);
    }

    /// <summary>Checks all three paths before profiling.</summary>
    /// <param name="state">The application corpora and analyzer.</param>
    /// <param name="startup">The startup corpus.</param>
    /// <param name="nodes">The expected number of violations.</param>
    /// <returns>A task representing validation.</returns>
    internal static async Task ValidateAsync(SingleAnalyzerBenchmarkState state, AnalyzerBenchmarkScenario startup, int nodes)
    {
        await ValidateScenarioAsync(startup, state.Analyzers, 0).ConfigureAwait(false);
        await ValidateScenarioAsync(state.CleanScenario, state.Analyzers, 0).ConfigureAwait(false);
        await ValidateScenarioAsync(state.ViolatingScenario, state.Analyzers, nodes).ConfigureAwait(false);
    }

    /// <summary>Rejects compiler errors and mislabeled diagnostic counts.</summary>
    /// <param name="scenario">The input compilation.</param>
    /// <param name="analyzers">The analyzer to execute.</param>
    /// <param name="expectedCount">The required diagnostic count.</param>
    /// <returns>A task representing validation.</returns>
    /// <exception cref="InvalidOperationException">The corpus is invalid.</exception>
    private static async Task ValidateScenarioAsync(AnalyzerBenchmarkScenario scenario, ImmutableArray<DiagnosticAnalyzer> analyzers, int expectedCount)
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
            throw new InvalidOperationException($"Expected {expectedCount} diagnostics, found {actualCount}.");
        }
    }
}
