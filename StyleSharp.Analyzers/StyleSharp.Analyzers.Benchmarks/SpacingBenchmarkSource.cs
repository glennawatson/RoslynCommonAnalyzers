// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for spacing-analyzer token-walk benchmarks.</summary>
internal static class SpacingBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating spacing patterns.</summary>
    /// <param name="types">The number of types to emit.</param>
    /// <param name="violating">Whether to emit compact spacing that trips the analyzer.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating
               ? $$"""
                 public class C{{i}}{
                     public int M(int x,int y){
                         if(x>y){
                             return x+y;
                         }
                         return x-y;
                     }
                 }
                 """
               : $$"""
                 public class C{{i}}
                 {
                     public int M(int x, int y)
                     {
                         if (x > y)
                         {
                             return x + y;
                         }

                         return x - y;
                     }
                 }
                 """)}}
           """;
}
