// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Builds synthetic source for file-type/namespace analyzer (SST1402) benchmarks. The clean
/// corpus keeps a single top-level type (nested types are ignored by the rule); the violating
/// corpus emits many top-level types so every type beyond the first is reported.
/// </summary>
internal static class FileTypeNamespaceBenchmarkSource
{
    /// <summary>Builds a compilation unit with either one or many top-level types.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit multiple top-level types (rule violations).</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => violating ? GenerateViolating(types) : GenerateClean(types);

    /// <summary>Builds a single top-level type containing many nested types (no violations).</summary>
    /// <param name="types">The number of nested types to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateClean(int types)
        => $$"""
           namespace Bench;

           public sealed class Root
           {
               {{BenchmarkSourceText.JoinBlocks(types, GenerateNestedType)}}
           }
           """;

    /// <summary>Builds many top-level types (every type beyond the first violates SST1402).</summary>
    /// <param name="types">The number of top-level types to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateViolating(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateTopLevelType)}}
           """;

    /// <summary>Builds one nested type for the clean corpus.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated nested type block.</returns>
    private static string GenerateNestedType(int index)
        => $$"""
           public sealed class Nested{{index}}
           {
               public int Value { get; set; }
           }
           """;

    /// <summary>Builds one top-level type for the violating corpus.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated top-level type block.</returns>
    private static string GenerateTopLevelType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Value { get; set; }
           }
           """;
}
