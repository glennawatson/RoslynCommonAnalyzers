// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for case-beside-default analysis.</summary>
internal static class RemoveCaseBesideDefaultBenchmarkSource
{
    /// <summary>The number of switch shapes cycled by this source generator.</summary>
    private const int SwitchShapeCount = 2;

    /// <summary>Builds clean or violating switch members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable switches.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class RemoveCaseBesideDefaultBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % SwitchShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public int Route{{index}}(int value)
                           {
                               switch (value)
                               {
                                   case 1:
                                       return 1;
                                   case 2:
                                   default:
                                       return 0;
                               }
                           }
                           """,
            (1, true) => $$"""
                           public int Route{{index}}(int value)
                           {
                               switch (value)
                               {
                                   case 1:
                                       return 1;
                                   case 2:
                                   case 3:
                                   default:
                                       return 0;
                               }
                           }
                           """,
            (0, false) => $$"""
                            public int Route{{index}}(int value)
                            {
                                switch (value)
                                {
                                    case 1:
                                        return 1;
                                    default:
                                        return 0;
                                }
                            }
                            """,
            _ => $$"""
                   public int Route{{index}}(int value)
                   {
                       switch (value)
                       {
                           case 2:
                           case 3:
                               return 1;
                           default:
                               return 0;
                       }
                   }
                   """
        };
}
