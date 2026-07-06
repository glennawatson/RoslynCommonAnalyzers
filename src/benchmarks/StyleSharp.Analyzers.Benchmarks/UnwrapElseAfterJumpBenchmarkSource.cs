// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for unwrap-else-after-jump analysis.</summary>
internal static class UnwrapElseAfterJumpBenchmarkSource
{
    /// <summary>The number of branch shapes cycled by this source generator.</summary>
    private const int BranchShapeCount = 2;

    /// <summary>Builds clean or violating if/else members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable else clauses.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class UnwrapElseAfterJumpBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % BranchShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public int Block{{index}}(int value)
                           {
                               if (value > 0)
                               {
                                   return 1;
                               }
                               else
                               {
                                   return 2;
                               }
                           }
                           """,
            (1, true) => $$"""
                           public int Chain{{index}}(int value)
                           {
                               if (value > 0)
                               {
                                   return 1;
                               }
                               else if (value < 0)
                               {
                                   return 2;
                               }

                               return 3;
                           }
                           """,
            (0, false) => $$"""
                            public int Block{{index}}(int value)
                            {
                                var result = 0;
                                if (value > 0)
                                {
                                    result = 1;
                                }
                                else
                                {
                                    result = 2;
                                }

                                return result;
                            }
                            """,
            _ => $$"""
                   public int Chain{{index}}(int value)
                   {
                       if (value > 0)
                       {
                           return 1;
                       }

                       return 3;
                   }
                   """
        };
}
