// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the pattern-matching analyzer benchmarks.</summary>
internal static class PatternMatchingBenchmarkSource
{
    /// <summary>The number of violating pattern-matching shapes cycled by the source generator.</summary>
    private const int PatternShapeCount = 2;

    /// <summary>Builds a compilation unit of clean or violating null comparisons.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit 'as' casts compared to null.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class PatternMatchingBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit an older pattern-matching idiom.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
    {
        if (!violating)
        {
            return $"internal static bool M{index}(string x) => x is not null;";
        }

        return index % PatternShapeCount == 0
            ? $$"""
               internal static bool M{{index}}(object x) => (x as string) != null;
               """
            : $$"""
               internal static int M{{index}}(object x)
               {
                   if (x is string)
                   {
                       var text = (string)x;
                       return text.Length;
                   }

                   return 0;
               }
               """;
    }
}
