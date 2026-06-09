// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for inheritdoc-validity analysis (SST1648).</summary>
internal static class Sst1648InheritDocBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1648InheritDocAnalyzer(),
            Sst1648InheritDocBenchmarkSource.Generate,
            nodes);
}
