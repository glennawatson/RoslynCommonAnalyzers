// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for member-ordering analyzer benchmarks.</summary>
internal static class MemberOrderingBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating member-order patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit member-ordering violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit ordering violations.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public const int A = 0;
               public static readonly int B = 1;
               public static int C = 2;
               public readonly int D = 3;
               public int E;

               public C{{index}}()
               {
               }

               public int P { get; } = 4;

               public void M()
               {
               }

               public sealed class Nested
               {
               }
           }
           """;

    /// <summary>Builds one violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int E;
               public const int A = 0;
               public static int C = 2;
               public readonly int D = 3;

               public C{{index}}()
               {
               }

               public int P { get; } = 4;

               public void M()
               {
               }
           }
           """;
}
