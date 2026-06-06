// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for parameter-list layout analyzer benchmarks.</summary>
internal static class ParameterListLayoutBenchmarkSource
{
    /// <summary>Builds a compilation unit containing many parameter and argument lists.</summary>
    /// <param name="members">The number of method/call pairs to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;
           internal static class ParameterLayoutBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => violating
               ? $$"""
                 private static int Add{{i}}(
                     int x,

                     int y
                 ) => x + y;

                 internal static int Use{{i}}()
                     => Add{{i}}(
                         1 +
                             2);
                 """
               : $$"""
                 private static int Add{{i}}(
                     int x,
                     int y) => x + y;

                 internal static int Use{{i}}()
                     => Add{{i}}(
                         1,
                         2);
                 """)}}
           }
           """;
}
