// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for rethrow-only catch analysis.</summary>
internal static class RethrowOnlyCatchBenchmarkSource
{
    /// <summary>The number of try/catch shapes cycled by this source generator.</summary>
    private const int CatchShapeCount = 2;

    /// <summary>Builds clean or violating try/catch members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable catch clauses.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class RethrowOnlyCatchBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % CatchShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public int Trailing{{index}}(int value)
                           {
                               try
                               {
                                   return value + 1;
                               }
                               catch (System.InvalidOperationException)
                               {
                                   return -1;
                               }
                               catch (System.IO.IOException)
                               {
                                   throw;
                               }
                           }
                           """,
            (1, true) => $$"""
                           public int Sole{{index}}(int value)
                           {
                               try
                               {
                                   return value + 1;
                               }
                               catch
                               {
                                   throw;
                               }
                           }
                           """,
            (0, false) => $$"""
                            public int Trailing{{index}}(int value)
                            {
                                try
                                {
                                    return value + 1;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    return -1;
                                }
                                catch (System.IO.IOException)
                                {
                                    return -2;
                                }
                            }
                            """,
            _ => $$"""
                   public int Sole{{index}}(int value)
                   {
                       try
                       {
                           return value + 1;
                       }
                       catch (System.IO.IOException)
                       {
                           throw;
                       }
                       catch (System.Exception)
                       {
                           return -1;
                       }
                   }
                   """
        };
}
