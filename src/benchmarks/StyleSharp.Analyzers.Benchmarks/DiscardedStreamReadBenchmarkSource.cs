// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for discarded-stream-read analyzer benchmarks (SST2446).</summary>
internal static class DiscardedStreamReadBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating stream reads.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.IO;
           using System.Threading.Tasks;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose reads all use their count.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public async Task<int> Read(Stream stream, Memory<byte> buffer)
               {
                   int read = await stream.ReadAsync(buffer).ConfigureAwait(false);
                   return read;
               }

               public async Task Bare(Stream stream, Memory<byte> buffer)
               {
                   await stream.ReadAsync(buffer);
               }
           }
           """;

    /// <summary>Builds one type whose reads discard the count through a configured awaiter.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public async Task Read(Stream stream, Memory<byte> buffer)
               {
                   await stream.ReadAsync(buffer).ConfigureAwait(false);
               }
           }
           """;
}
