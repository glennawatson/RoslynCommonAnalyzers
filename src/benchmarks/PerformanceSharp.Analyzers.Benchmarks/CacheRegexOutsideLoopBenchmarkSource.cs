// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the regex-in-loop analyzer benchmarks.</summary>
internal static class CacheRegexOutsideLoopBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises the clean or the violating shape.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Text.RegularExpressions;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type that matches through a cached instance.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private static readonly Regex Word = new Regex("[a-z]+");

               public int Count(string[] values)
               {
                   var count = 0;
                   foreach (var value in values)
                   {
                       if (Word.IsMatch(value))
                       {
                           count++;
                       }
                   }

                   return count;
               }
           }
           """;

    /// <summary>Builds one type that calls the static overloads from inside a loop.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Count(string[] values)
               {
                   var count = 0;
                   foreach (var value in values)
                   {
                       if (Regex.IsMatch(value, "[a-z]+"))
                       {
                           count++;
                       }
                   }

                   return count;
               }
           }
           """;
}
