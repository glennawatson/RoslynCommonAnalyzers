// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds benchmark state for static-local-function analysis.</summary>
internal static class StaticLocalFunctionBenchmarkCases
{
    /// <summary>Creates prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new Sst2235StaticLocalFunctionAnalyzer(), StaticLocalFunctionBenchmarkSource.Generate, nodes);
}
