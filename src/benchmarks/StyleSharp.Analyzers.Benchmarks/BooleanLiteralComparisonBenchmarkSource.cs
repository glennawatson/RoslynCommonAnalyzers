// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for boolean-literal-comparison analyzer benchmarks.</summary>
internal static class BooleanLiteralComparisonBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating boolean comparisons.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit boolean-literal comparison violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class BooleanComparisonBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a boolean-literal comparison violation.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating ? GenerateViolatingMember(index) : GenerateCleanMember(index);

    /// <summary>Builds one clean member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCleanMember(int index)
        => $$"""
           internal static bool M{{index}}(bool left, bool right) => left == right;
           """;

    /// <summary>Builds one violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           internal static bool M{{index}}(bool left) => left == true;
           """;
}
