// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for use-direct-regex-queries analyzer benchmarks.</summary>
internal static class UseDirectRegexQueriesBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating regex query patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit use-direct-regex-queries rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating regex query type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean regex query type that asks the direct question.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private static readonly System.Text.RegularExpressions.Regex Pattern = new System.Text.RegularExpressions.Regex("a+");

               public bool M(string input)
               {
                   return Pattern.IsMatch(input);
               }
           }
           """;

    /// <summary>Builds one violating regex query type that materializes a match for a bool.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private static readonly System.Text.RegularExpressions.Regex Pattern = new System.Text.RegularExpressions.Regex("a+");

               public bool M(string input)
               {
                   return Pattern.Match(input).Success;
               }
           }
           """;
}
