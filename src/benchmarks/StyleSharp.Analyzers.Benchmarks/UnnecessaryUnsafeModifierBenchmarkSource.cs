// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for unnecessary-unsafe-modifier analysis.</summary>
internal static class UnnecessaryUnsafeModifierBenchmarkSource
{
    /// <summary>Builds clean or violating unsafe-modifier members.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit redundant unsafe modifiers.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class UnnecessaryUnsafeModifierBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member source.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating
            ? $"public unsafe int M{index}(int value) => value + {index};"
            : $"public unsafe int M{index}(int* value) => *value + {index};";
}
