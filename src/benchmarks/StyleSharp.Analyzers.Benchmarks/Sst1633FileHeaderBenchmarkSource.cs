// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for file-header analyzer (SST1633) benchmarks.</summary>
internal static class Sst1633FileHeaderBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises a present or missing file header.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit a file missing its configured header.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => violating
            ? $$"""
              namespace Bench;

              {{BenchmarkSourceText.JoinBlocks(types, GenerateType)}}
              """
            : $$"""
              // Copyright text.
              namespace Bench;

              {{BenchmarkSourceText.JoinBlocks(types, GenerateType)}}
              """;

    /// <summary>Builds one well-formed type to give the file realistic content.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index)
        => $$"""
           internal class C{{index}}
           {
               public void M()
               {
               }
           }
           """;
}
