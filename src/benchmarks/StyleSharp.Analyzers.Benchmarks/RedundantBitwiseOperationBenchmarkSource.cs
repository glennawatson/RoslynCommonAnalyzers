// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for redundant-bitwise-operation (SST1481) analyzer benchmarks.</summary>
internal static class RedundantBitwiseOperationBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating bitwise patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit redundant-bitwise rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating bitwise type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose bitwise operations all do something.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: an operand shape that can never be
    /// constant, a boolean operation with no integral type, a mask that is not an identity, and two
    /// operands that are not constant at all.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private const int Mask = 0x0F;

               public int Keep(int value) => value & Mask;

               public int Toggle(int value) => value ^ Mask;

               public int Combine(int left, int right) => left | right;

               public bool Both(bool left, bool right) => left & right;

               public int Narrowing(byte value) => value & 0xFF;

               public int Indexed(int value, int[] masks) => value | masks[0];
           }
           """;

    /// <summary>Builds one type whose bitwise operations cannot change their operand.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Or(int value) => value | 0;

               public int Xor(int value) => value ^ 0;

               public int AndAllBits(int value) => value & ~0;

               public int AndZero(int value) => value & 0;

               public long LeftConstant(long value) => 0 | value;
           }
           """;
}
