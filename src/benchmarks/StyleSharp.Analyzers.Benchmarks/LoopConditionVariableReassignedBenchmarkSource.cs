// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for loop-condition-variable-reassignment analyzer benchmarks (SST2465).</summary>
internal static class LoopConditionVariableReassignedBenchmarkSource
{
    /// <summary>Builds a compilation unit whose for loops are all clean or all reassign a condition variable.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose counted loop only reads its condition variables.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Sum(int n)
               {
                   var total = 0;
                   for (var i = 0; i < n; i++)
                   {
                       total += i;
                   }

                   return total;
               }
           }
           """;

    /// <summary>Builds one type whose loop body reassigns the bound the condition tests.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Sum(int n)
               {
                   var total = 0;
                   for (var i = 0; i < n; i++)
                   {
                       total += i;
                       n = 0;
                   }

                   return total;
               }
           }
           """;
}
