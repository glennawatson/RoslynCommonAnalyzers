// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1423 too-many-switch-labels analyzer benchmarks.</summary>
internal static class Sst1423TooManySwitchLabelsBenchmarkSource
{
    /// <summary>The number of switch sections a clean type emits (at or below the default maximum).</summary>
    private const int CleanSectionCount = 4;

    /// <summary>The number of switch sections a violating type emits (above the default maximum of 30).</summary>
    private const int ViolatingSectionCount = 34;

    /// <summary>Builds a compilation unit that exercises clean or violating switch-section counts.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit too-many-switch-section violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating switch-section type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean switch type whose section count is at or below the default maximum.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int M(int value)
               {
                   switch (value)
                   {
           {{GenerateSections(CleanSectionCount)}}
                       default:
                           return -1;
                   }
               }
           }
           """;

    /// <summary>Builds one violating switch type whose section count exceeds the default maximum.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int M(int value)
               {
                   switch (value)
                   {
           {{GenerateSections(ViolatingSectionCount)}}
                       default:
                           return -1;
                   }
               }
           }
           """;

    /// <summary>Builds the requested number of distinct case sections.</summary>
    /// <param name="count">The number of sections to emit.</param>
    /// <returns>The generated section text.</returns>
    private static string GenerateSections(int count)
        => BenchmarkSourceText.JoinLines(
            count,
            i => $"                case {i}:\n                    return {i};");
}
