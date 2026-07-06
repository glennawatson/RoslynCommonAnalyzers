// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for short-circuit-operator analysis.</summary>
internal static class ShortCircuitOperatorBenchmarkSource
{
    /// <summary>The number of operator shapes cycled by this source generator.</summary>
    private const int OperatorShapeCount = 3;

    /// <summary>The cycled index of the third operator shape.</summary>
    private const int ThirdShapeIndex = 2;

    /// <summary>Builds clean or violating boolean-operator members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable operators.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class ShortCircuitOperatorBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % OperatorShapeCount, violating) switch
        {
            (0, true) => $"public bool And{index}(bool left, bool right) => left & right;",
            (1, true) => $"public bool Or{index}(bool left, bool right) => left | right;",
            (ThirdShapeIndex, true) => $"public bool Compare{index}(bool left, int value) => left & value == {index};",
            (0, false) => $"public bool And{index}(bool left, bool right) => left && right;",
            (1, false) => $"public int Mask{index}(int flags) => flags & {index};",
            _ => $$"""
                   public bool Call{{index}}(bool value) => value & Flip{{index}}(value);

                   private static bool Flip{{index}}(bool value) => !value;
                   """,
        };
}
