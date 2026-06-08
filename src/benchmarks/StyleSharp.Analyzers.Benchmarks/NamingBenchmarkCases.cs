// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for naming analyzer benchmark suites.</summary>
internal static class NamingBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for parameter naming analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateParameter(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new Sst1313ParameterNamingAnalyzer(), NamingBenchmarkSource.GenerateParameterSource, nodes);

    /// <summary>Creates the prepared benchmark state for local-variable naming analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateLocalVariable(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new Sst1312LocalVariableNamingAnalyzer(), NamingBenchmarkSource.GenerateLocalVariableSource, nodes);

    /// <summary>Creates the prepared benchmark state for field naming analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateField(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new FieldNamingAnalyzer(), NamingBenchmarkSource.GenerateFieldSource, nodes);

    /// <summary>Creates the prepared benchmark state for element naming analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateElement(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new Sst1300ElementNamingAnalyzer(), NamingBenchmarkSource.GenerateElementSource, nodes);
}
