// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for fused-SQL-keyword analyzer benchmarks (SST2470).</summary>
internal static class FusedSqlKeywordBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating string concatenations.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose concatenation keeps a space at the seam.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public static class C{{index}}
           {
               public static string Build()
                   => "SELECT * FROM t" + " WHERE id = 1";
           }
           """;

    /// <summary>Builds one type whose concatenation fuses a keyword at the seam.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public static class V{{index}}
           {
               public static string Build()
                   => "SELECT * FROM t" + "WHERE id = 1";
           }
           """;
}
