// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for unchecked-sequence-sum analyzer benchmarks (SST2457).</summary>
internal static class UncheckedSequenceSumBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating Sum calls.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Linq;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose Sum calls and unchecked wrappers never combine.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a bare Sum (the enclosing-unchecked
    /// walk rejects), a non-Sum call inside unchecked (the name gate rejects, which must not bind), a
    /// double Sum inside unchecked (the bind rejects on return type), and a user-defined Sum inside
    /// unchecked (the bind rejects on containing type).
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Plain(int[] values) => values.Sum();

               public int Largest(int[] values) => unchecked(values.Max() + 1);

               public double Floating(double[] values) => unchecked(values.Sum());

               public int Own() => unchecked(Sum() + 1);

               public int Sum() => 0;
           }
           """;

    /// <summary>Builds one type whose integral Sum calls sit inside unchecked wrappers.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Total(int[] values) => unchecked(values.Sum());

               public long Block(long[] values)
               {
                   unchecked
                   {
                       return values.Sum() + 1;
                   }
               }
           }
           """;
}
