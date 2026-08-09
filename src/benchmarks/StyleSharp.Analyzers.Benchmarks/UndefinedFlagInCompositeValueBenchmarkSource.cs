// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the undefined-composite-flag analyzer benchmarks.</summary>
internal static class UndefinedFlagInCompositeValueBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises the clean or the violating shape.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one flags enum whose composite uses only declared bits.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           [System.Flags]
           public enum Clean{{index}}
           {
               None = 0,
               Read = 1,
               Write = 2,
               Execute = 4,
               All = 7,
           }
           """;

    /// <summary>Builds one flags enum whose composite sets a bit no member declares.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           [System.Flags]
           public enum Violating{{index}}
           {
               None = 0,
               Read = 1,
               Write = 2,
               All = 7,
           }
           """;
}
