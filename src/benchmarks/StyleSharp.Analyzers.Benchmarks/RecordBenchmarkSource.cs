// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for record analyzer benchmarks.</summary>
internal static class RecordBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating record patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit record rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating record declaration.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating record declaration.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean record declaration.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public readonly record struct Sample{{index}}(int Value)
           {
               public string Name { get; init; } = string.Empty;
           }
           """;

    /// <summary>Builds one violating record declaration.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public record struct Sample{{index}}(int value)
           {
               public string Name { get; set; } = string.Empty;
           }
           """;
}
