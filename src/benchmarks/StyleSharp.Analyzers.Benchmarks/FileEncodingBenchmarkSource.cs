// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Builds synthetic source for file-encoding analyzer (SST1412) benchmarks. The analyzer
/// inspects the parsed tree's text encoding once per file, so the corpus is scaled by member
/// count to exercise tree parsing while keeping a single syntax-tree diagnostic site.
/// </summary>
internal static class FileEncodingBenchmarkSource
{
    /// <summary>Builds a compilation unit whose member count scales with the requested node count.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit the structurally larger (violating) shape.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating file-encoding type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit the structurally larger type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one minimal clean type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Value { get; set; }
           }
           """;

    /// <summary>Builds one structurally larger type for the violating corpus.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int First { get; set; }

               public int Second { get; set; }

               public int Compute() => First + Second;
           }
           """;
}
