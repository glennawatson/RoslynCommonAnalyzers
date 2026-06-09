// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for the opt-in SST1305 Hungarian-notation analyzer.</summary>
internal static class Sst1305HungarianNotationBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for the requested node count, forcing the opt-in rule on.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new Sst1305HungarianNotationAnalyzer(), Sst1305HungarianNotationBenchmarkSource.Generate, nodes, ["SST1305"]);
}
