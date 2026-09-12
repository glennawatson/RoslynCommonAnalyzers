// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if ROSLYN_5_9_OR_GREATER

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for boxing-union-case analysis.</summary>
internal static class Psh1025BoxingUnionCaseBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SingleAnalyzerBenchmarkState Create(int nodes) =>
        SingleAnalyzerBenchmarkHelper.Create(
            new Psh1025BoxingUnionCaseAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(Psh1025BoxingUnionCaseBenchmarkSource.Generate(nodes, violating: false)).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(Psh1025BoxingUnionCaseBenchmarkSource.Generate(nodes, violating: true)).Compilation));
}

#endif
