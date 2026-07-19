// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for the constant-AEAD-nonce analysis.</summary>
internal static class ConstantAeadNonceBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for the requested type count.</summary>
    /// <param name="types">The synthetic type count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int types)
        => SingleAnalyzerBenchmarkHelper.Create(
            new Ses1001ConstantAeadNonceAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(ConstantAeadNonceBenchmarkSource.Generate(types, violating: false)).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(ConstantAeadNonceBenchmarkSource.Generate(types, violating: true)).Compilation));
}
