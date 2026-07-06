// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for manual-enumerator loop analysis.</summary>
internal static class ForeachOverManualEnumeratorBenchmarkSource
{
    /// <summary>The number of loop shapes cycled by this source generator.</summary>
    private const int LoopShapeCount = 2;

    /// <summary>Builds clean or violating manual-enumerator members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable loops.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           using System.Collections.Generic;

           namespace Bench;

           internal sealed class ForeachOverManualEnumeratorBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % LoopShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public int Sum{{index}}(List<int> values)
                           {
                               var total = 0;
                               var walker = values.GetEnumerator();
                               while (walker.MoveNext())
                               {
                                   var value = walker.Current;
                                   total += value;
                               }

                               return total;
                           }
                           """,
            (1, true) => $$"""
                           public int Count{{index}}(List<string> values)
                           {
                               var total = 0;
                               var walker = values.GetEnumerator();
                               while (walker.MoveNext())
                               {
                                   total += walker.Current.Length;
                               }

                               return total;
                           }
                           """,
            (0, false) => $$"""
                            public int Sum{{index}}(List<int> values)
                            {
                                var total = 0;
                                var walker = values.GetEnumerator();
                                while (walker.MoveNext())
                                {
                                    total += walker.Current;
                                }

                                walker.Dispose();
                                return total;
                            }
                            """,
            _ => $$"""
                   public int Count{{index}}(List<string> values)
                   {
                       var total = 0;
                       var walker = values.GetEnumerator();
                       while (walker.MoveNext())
                       {
                           total += walker.Current.Length;
                           walker.Dispose();
                       }

                       return total;
                   }
                   """
        };
}
