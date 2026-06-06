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
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating method/call pair.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating ? GenerateViolatingMember(index) : GenerateCleanMember(index);

    /// <summary>Builds one clean method/call pair.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCleanMember(int index)
        => $$"""
           private static int Add{{index}}(
               int x,
               int y) => x + y;

           internal static int Use{{index}}()
               => Add{{index}}(
                   1,
                   2);
           """;

    /// <summary>Builds one violating method/call pair.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           private static int Add{{index}}(
               int x,

               int y
           ) => x + y;

           internal static int Use{{index}}()
               => Add{{index}}(
                   1 +
                       2);
           """;
}
