// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for invariant-loop-condition analyzer benchmarks (SST2406).</summary>
internal static class InvariantLoopConditionBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating loops.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Collections.Generic;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose loops can all reach their stop condition.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a loop that advances its counter, one that
    /// breaks out, a condition holding a call the rule refuses to reason about, and a deliberate
    /// <c>while (true)</c>.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public void Advance(int limit)
               {
                   var index = 0;
                   while (index < limit)
                   {
                       Console.WriteLine(index);
                       index++;
                   }

                   for (var i = 0; i < limit; i++)
                   {
                       Console.WriteLine(i);
                   }
               }

               public void Drain(Queue<int> queue, int limit)
               {
                   while (queue.Count > 0)
                   {
                       Console.WriteLine(queue.Dequeue());
                   }

                   var index = 0;
                   while (index < limit)
                   {
                       break;
                   }

                   while (true)
                   {
                       return;
                   }
               }
           }
           """;

    /// <summary>Builds one type whose loop can never change its stop condition.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Spin(int limit)
               {
                   var index = 0;
                   while (index < limit)
                   {
                       Console.WriteLine(index);
                   }
               }
           }
           """;
}
