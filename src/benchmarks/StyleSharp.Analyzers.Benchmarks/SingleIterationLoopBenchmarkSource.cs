// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for SST1444 single-iteration-loop benchmarks.</summary>
internal static class SingleIterationLoopBenchmarkSource
{
    /// <summary>Builds a compilation unit with clean or violating loop cases.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit single-iteration-loop violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one synthetic type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit single-iteration-loop violations.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds a type with loops that can naturally iterate again.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class LoopClean{{index}}
           {
               public int Sum(int[] values)
               {
                   var total = 0;
                   foreach (var value in values)
                   {
                       if (value < 0)
                       {
                           continue;
                       }

                       while (value > total)
                       {
                           total++;
                       }

                       total += value;
                   }

                   return total;
               }
           }
           """;

    /// <summary>Builds a type with unconditional jumps that prevent a second iteration.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class LoopViolation{{index}}
           {
               public int First(int[] values)
               {
                   foreach (var value in values)
                   {
                       return value;
                   }

                   return 0;
               }

               public int Stop(int[] values)
               {
                   var total = 0;
                   while (total < values.Length)
                   {
                       break;
                   }

                   return total;
               }
           }
           """;
}
