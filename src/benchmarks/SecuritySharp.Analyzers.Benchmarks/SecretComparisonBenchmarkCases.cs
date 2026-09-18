// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Prepares and validates the secret comparison benchmark corpora.</summary>
internal static class SecretComparisonBenchmarkCases
{
    /// <summary>Creates matching ordinary and secret-bearing comparison corpora.</summary>
    /// <param name="types">The number of generated comparison types.</param>
    /// <returns>The analyzer and compiled corpora.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SingleAnalyzerBenchmarkState Create(int types) =>
        SingleAnalyzerBenchmarkHelper.Create(
            new Ses1005NonConstantTimeSecretComparisonAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(SecretComparisonBenchmarkSource.Generate(types, violating: false)).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(SecretComparisonBenchmarkSource.Generate(types, violating: true)).Compilation));

    /// <summary>Creates an empty compilation with SES1005 explicitly enabled.</summary>
    /// <returns>The startup corpus.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AnalyzerBenchmarkScenario CreateStartup() =>
        new(BenchmarkCompilationFactory.CreateCompilation("public class Empty { }", ["SES1005"]).Compilation);

    /// <summary>Checks that a corpus compiles and exercises the intended diagnostic path.</summary>
    /// <param name="scenario">The prepared corpus.</param>
    /// <param name="analyzers">The analyzer under measurement.</param>
    /// <param name="expectedCount">The required diagnostic count.</param>
    /// <returns>A task representing the asynchronous validation.</returns>
    /// <exception cref="InvalidOperationException">The corpus does not compile or produces an unexpected diagnostic count.</exception>
    internal static async Task ValidateAsync(AnalyzerBenchmarkScenario scenario, ImmutableArray<DiagnosticAnalyzer> analyzers, int expectedCount)
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
            throw new InvalidOperationException($"SES1005 benchmark expected {expectedCount} diagnostics but produced {actualCount}.");
        }
    }
}
