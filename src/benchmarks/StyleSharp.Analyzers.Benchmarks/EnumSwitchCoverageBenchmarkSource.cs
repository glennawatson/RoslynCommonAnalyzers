// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for enum switch coverage analysis.</summary>
internal static class EnumSwitchCoverageBenchmarkSource
{
    /// <summary>The number of enum switch shapes cycled by this source generator.</summary>
    private const int SwitchShapeCount = 2;

    /// <summary>Builds clean or violating enum switch members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable switches.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal enum Mode
           {
               First,
               Second,
               Third
           }

           internal sealed class EnumSwitchCoverageBench
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
                           public int Statement{{index}}(Mode mode)
                           {
                               switch (mode)
                               {
                                   case Mode.First:
                                       return 1;
                               }

                               return 0;
                           }
                           """,
            (1, true) => $$"""
                           public int Expression{{index}}(Mode mode) => mode switch
                           {
                               Mode.First => 1,
                               Mode.Second => 2
                           };
                           """,
            (0, false) => $$"""
                            public int Statement{{index}}(Mode mode)
                            {
                                switch (mode)
                                {
                                    case Mode.First:
                                        return 1;
                                    default:
                                        return 0;
                                }
                            }
                            """,
            _ => $$"""
                   public int Expression{{index}}(Mode mode) => mode switch
                   {
                       Mode.First => 1,
                       _ => 0
                   };
                   """
        };
}
