// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for layout and declaration analyzer benchmarks.</summary>
internal static class LayoutHotspotBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating layout patterns.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit violating patterns.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => violating ? GenerateViolatingSource(members) : GenerateCleanSource(members);

    /// <summary>Builds the clean benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateCleanSource(int members)
        => $$"""
           namespace Bench;

           internal sealed class LayoutBench
           {

           {{BenchmarkSourceText.JoinBlocks(members, GenerateCleanMember)}}
           }
           """;

    /// <summary>Builds the violating benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateViolatingSource(int members)
        => $$"""
           namespace Bench;

           sealed class LayoutBench{
           {{BenchmarkSourceText.JoinLines(members, GenerateViolatingMember)}}
           }
           """;

    /// <summary>Builds one clean member block.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCleanMember(int index)
        => $$"""
           private int _value{{index}};

           internal int M{{index}}(int value)
           {
               if (value > 0)
               {
                   return value;
               }

               return -value;
           }
           """;

    /// <summary>Builds one violating member block.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           int _value{{index}};
           int M{{index}}(int value){if (value > 0) { return value; } return -value;}
           """;
}
