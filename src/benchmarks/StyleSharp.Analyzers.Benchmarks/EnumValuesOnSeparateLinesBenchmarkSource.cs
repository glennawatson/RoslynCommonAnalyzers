// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for enum-values-on-separate-lines analyzer benchmarks (SST1136).</summary>
internal static class EnumValuesOnSeparateLinesBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating enum-member-line patterns.</summary>
    /// <param name="types">The number of synthetic enums to emit.</param>
    /// <param name="violating">Whether to emit enum-member-line rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating enum-member-line type.</summary>
    /// <param name="index">The synthetic enum index.</param>
    /// <param name="violating">Whether to emit a violating enum.</param>
    /// <returns>The generated enum block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean enum whose members are each on their own line.</summary>
    /// <param name="index">The synthetic enum index.</param>
    /// <returns>The generated enum block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           internal enum E{{index}}
           {
               A,
               B,
               C,
           }
           """;

    /// <summary>Builds one violating enum whose members share a line.</summary>
    /// <param name="index">The synthetic enum index.</param>
    /// <returns>The generated enum block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           internal enum E{{index}}
           {
               A, B, C
           }
           """;
}
