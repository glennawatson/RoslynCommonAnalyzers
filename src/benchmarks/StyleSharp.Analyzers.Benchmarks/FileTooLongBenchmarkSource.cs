// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for file-length analyzer benchmarks.</summary>
/// <remarks>
/// A file is one diagnostic at most, so the two scenarios share one corpus and differ only in the configured
/// maximum: the clean run is turned away by the raw line-count bound, the violating run walks every token.
/// </remarks>
internal static class FileTooLongBenchmarkSource
{
    /// <summary>Builds a compilation unit of the requested size.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateType)}}
           """;

    /// <summary>Builds one type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               // A comment costs the file nothing: it is not a code line.
               public int Value { get; set; }

               public int Doubled() => Value * 2;
           }
           """;
}
