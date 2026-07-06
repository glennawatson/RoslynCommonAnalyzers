// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for collapse-else-into-else-if analysis.</summary>
internal static class CollapseElseIntoElseIfBenchmarkSource
{
    /// <summary>The number of else shapes cycled by this source generator.</summary>
    private const int ElseShapeCount = 2;

    /// <summary>Builds clean or violating else-clause members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable else blocks.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class CollapseElseIntoElseIfBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % ElseShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public int Wrapped{{index}}(int value)
                           {
                               if (value > 0)
                               {
                                   return 1;
                               }
                               else
                               {
                                   if (value < 0)
                                   {
                                       return -1;
                                   }
                               }

                               return 0;
                           }
                           """,
            (1, true) => $$"""
                           public int Chained{{index}}(int value)
                           {
                               if (value > 0)
                               {
                                   return 1;
                               }
                               else
                               {
                                   if (value < 0)
                                   {
                                       return -1;
                                   }
                                   else
                                   {
                                       return 0;
                                   }
                               }
                           }
                           """,
            (0, false) => $$"""
                            public int Wrapped{{index}}(int value)
                            {
                                if (value > 0)
                                {
                                    return 1;
                                }
                                else if (value < 0)
                                {
                                    return -1;
                                }

                                return 0;
                            }
                            """,
            _ => $$"""
                   public int Chained{{index}}(int value)
                   {
                       if (value > 0)
                       {
                           return 1;
                       }
                       else
                       {
                           var next = value - 1;
                           if (next < 0)
                           {
                               return next;
                           }
                       }

                       return 0;
                   }
                   """
        };
}
