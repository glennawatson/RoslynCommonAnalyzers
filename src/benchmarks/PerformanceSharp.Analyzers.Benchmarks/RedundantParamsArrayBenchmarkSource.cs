// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for redundant-params-array analyzer benchmarks.</summary>
internal static class RedundantParamsArrayBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating params call sites.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class RedundantParamsArrayBench
           {
               public void Log(params object[] args)
               {
               }

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating call.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating
            ? $$"""
                public void Call{{index}}(object a, object b) => Log(new object[] { a, b });
                """
            : $$"""
                public void Call{{index}}(object a, object b) => Log(a, b);
                """;
}
