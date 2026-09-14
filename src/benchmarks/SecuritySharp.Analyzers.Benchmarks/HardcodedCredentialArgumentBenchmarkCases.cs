// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Prepares the credential argument analyzer and both benchmark paths.</summary>
internal static class HardcodedCredentialArgumentBenchmarkCases
{
    /// <summary>Builds the benchmark state for the requested input size.</summary>
    /// <param name="nodes">The number of generated types.</param>
    /// <returns>The prepared analyzer and compilations.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SingleAnalyzerBenchmarkState Create(int nodes) =>
        SingleAnalyzerBenchmarkHelper.Create(
            new Ses1202HardcodedCredentialArgumentAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(HardcodedCredentialArgumentBenchmarkSource.Generate(nodes, violating: false)).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(HardcodedCredentialArgumentBenchmarkSource.Generate(nodes, violating: true)).Compilation));
}
