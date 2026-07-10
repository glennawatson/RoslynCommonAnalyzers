// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for magic-number analyzer benchmarks.</summary>
internal static class MagicNumberBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating magic-number patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit magic-number rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating magic-number type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose literals are all allow-listed or exempt by position.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: an allow-listed value, a bit pattern,
    /// a shift distance, a cardinality guard, a named declaration and a buffer length.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private const int Limit = 90;

               private readonly int[] _values = { 0, 1 };

               public int Count { get; }

               public bool Any() => Count > 0;

               public bool Pair(int[] items) => items.Length == 2;

               public uint Mix(uint hash) => (hash >> 16) ^ (hash << 13) ^ 0xFF00FFu;

               public byte[] Buffer() => new byte[16];

               public int Scale(int value = 25) => (value * Limit) + _values.Length + Count;
           }
           """;

    /// <summary>Builds one type whose literals are all reported.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public bool Big(int value) => value > 90;

               public int Mix(int hash) => (hash * 397) ^ 17;

               public int Delay(int milliseconds) => milliseconds + 500;

               public int Case(int state) => state switch { 3 => 7, _ => 0 };

               public int Scale(int value) => value * 25;
           }
           """;
}
