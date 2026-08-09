// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the documentation element ordering analyzer benchmarks.</summary>
internal static class DocumentationElementOrderBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises the clean or the violating shape.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose documentation is already in the conventional order.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               /// <summary>Copies a range.</summary>
               /// <typeparam name="T">The element type.</typeparam>
               /// <param name="source">The source array.</param>
               /// <returns>The number copied.</returns>
               /// <remarks>Some background.</remarks>
               public int Copy<T>(T[] source) => source.Length;
           }
           """;

    /// <summary>Builds one type whose documentation elements are out of order.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               /// <summary>Copies a range.</summary>
               /// <returns>The number copied.</returns>
               /// <param name="source">The source array.</param>
               /// <typeparam name="T">The element type.</typeparam>
               /// <remarks>Some background.</remarks>
               public int Copy<T>(T[] source) => source.Length;
           }
           """;
}
