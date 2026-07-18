// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the new-member-reduces-accessibility analyzer benchmarks (SST2462).</summary>
internal static class NewMemberReducesAccessibilityBenchmarkSource
{
    /// <summary>Builds a compilation unit of base/derived pairs that exercise clean or violating hides.</summary>
    /// <param name="types">The number of synthetic base/derived pairs to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingPair(i) : GenerateCleanPair(i))}}
           """;

    /// <summary>Builds one base/derived pair whose derived member hides at equal accessibility, exercising the full walk without a report.</summary>
    /// <param name="index">The synthetic pair index.</param>
    /// <returns>The generated pair block.</returns>
    private static string GenerateCleanPair(int index)
        => $$"""
           public class Base{{index}}
           {
               public void M()
               {
               }
           }

           public class Derived{{index}} : Base{{index}}
           {
               public new void M()
               {
               }
           }
           """;

    /// <summary>Builds one base/derived pair whose derived member hides a more accessible base member with a narrower one.</summary>
    /// <param name="index">The synthetic pair index.</param>
    /// <returns>The generated pair block.</returns>
    private static string GenerateViolatingPair(int index)
        => $$"""
           public class Base{{index}}
           {
               public void M()
               {
               }
           }

           public class Derived{{index}} : Base{{index}}
           {
               private new void M()
               {
               }
           }
           """;
}
