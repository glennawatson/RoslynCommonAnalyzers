// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for suspicious-shift-count (SST1478) analyzer benchmarks.</summary>
internal static class SuspiciousShiftCountBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating shift patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit shift-count rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating shift type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose shift counts are all in range.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: the literal fast path that never touches
    /// the model, the 64-bit limit, the promotion of a narrow operand to an int, a native integer, and a
    /// count the compiler cannot fold.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Low(int value) => value << 1;

               public int High(int value) => value >> 31;

               public long Wide(long value) => value << 63;

               public int Promoted(byte value) => value << 0x08;

               public nint Native(nint value) => value << 64;

               public int Computed(int value, int count) => value << count;
           }
           """;

    /// <summary>Builds one type whose shift counts are all masked or meaningless.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Overshift(int value) => value << 32;

               public int Zero(int value) => value >> 0;

               public long WideOvershift(long value) => value << 64;

               public int Negative(int value) => value << -1;

               public uint Unsigned(uint value) => value >> 40;
           }
           """;
}
