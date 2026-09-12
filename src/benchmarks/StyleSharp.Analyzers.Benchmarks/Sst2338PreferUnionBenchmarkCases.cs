// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for the SST2338 prefer-union analyzer.</summary>
internal static class Sst2338PreferUnionBenchmarkCases
{
    /// <summary>The rule is opt-in, so the driver skips it unless the benchmark turns it on.</summary>
    private static readonly string[] EnabledRuleIds = ["SST2338"];

    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SingleAnalyzerBenchmarkState Create(int nodes) =>
        SingleAnalyzerBenchmarkCases.Create(new Sst2338PreferUnionAnalyzer(), Sst2338PreferUnionBenchmarkSource.Generate, nodes, EnabledRuleIds);
}
