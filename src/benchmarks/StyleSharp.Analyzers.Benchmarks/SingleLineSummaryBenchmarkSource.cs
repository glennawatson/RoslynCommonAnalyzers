// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for SST1653 analyzer benchmarks.</summary>
internal static class SingleLineSummaryBenchmarkSource
{
    /// <summary>Builds a compilation unit containing many documented types.</summary>
    /// <param name="types">The number of types to emit.</param>
    /// <param name="violating">Whether to emit avoidable multi-line summaries.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating documented type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit an avoidable multi-line summary.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean documented type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           /// <summary>Short summary {{index}}.</summary>
           public sealed class C{{index}}
           {
           }
           """;

    /// <summary>Builds one violating documented type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           /// <summary>
           /// Short summary {{index}}.
           /// </summary>
           public sealed class C{{index}}
           {
           }
           """;
}
