// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1503 require-braces analyzer benchmarks.</summary>
internal static class Sst1503RequireBracesBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating require-braces patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit require-braces rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating require-braces type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean require-braces type whose control-flow child is braced.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int M(bool x)
               {
                   if (x)
                   {
                       return {{index}};
                   }

                   return 0;
               }
           }
           """;

    /// <summary>Builds one violating require-braces type whose control-flow child omits its braces.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int M(bool x)
               {
                   if (x) return {{index}};
                   return 0;
               }
           }
           """;
}
