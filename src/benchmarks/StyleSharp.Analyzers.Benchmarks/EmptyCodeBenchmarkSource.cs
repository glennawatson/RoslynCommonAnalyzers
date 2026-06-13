// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the empty-code analyzer benchmarks.</summary>
internal static class EmptyCodeBenchmarkSource
{
    /// <summary>Builds a compilation unit of clean or violating type declarations.</summary>
    /// <param name="members">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit redundant empty constructors.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a redundant empty constructor.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating
            ? $"internal sealed class Bench{index} {{ public Bench{index}() {{ }} }}"
            : $"internal sealed class Bench{index} {{ public Bench{index}(int value) {{ }} }}";
}
