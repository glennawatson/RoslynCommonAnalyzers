// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for default-section-last analyzer benchmarks (SST1219).</summary>
internal static class DefaultSectionLastBenchmarkSource
{
    /// <summary>Builds a compilation unit whose switch places its default section last or not.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose switch ends with its default section.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int M(int x)
               {
                   switch (x)
                   {
                       case 1:
                           return 1;
                       default:
                           return 0;
                   }
               }
           }
           """;

    /// <summary>Builds one type whose default section precedes a case.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int M(int x)
               {
                   switch (x)
                   {
                       default:
                           return 0;
                       case 1:
                           return 1;
                   }
               }
           }
           """;
}
