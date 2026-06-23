// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for modern-syntax preference analysis.</summary>
internal static class ModernSyntaxPreferenceBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new ModernSyntaxPreferenceAnalyzer(), ModernSyntaxPreferenceBenchmarkSource.Generate, nodes);

    /// <summary>Creates the prepared benchmark state for one repeated benchmark shape.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes, ModernSyntaxPreferenceBenchmarkShape shape)
        => SingleAnalyzerBenchmarkCases.Create(
            new ModernSyntaxPreferenceAnalyzer(),
            (memberCount, violating) => ModernSyntaxPreferenceBenchmarkSource.Generate(memberCount, violating, shape),
            nodes);
}
