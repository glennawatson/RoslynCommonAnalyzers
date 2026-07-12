// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for integer-division-as-floating-point (SST1477) analyzer benchmarks.</summary>
internal static class IntegerDivisionBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating division patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit integer-division rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating division type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose divisions never truncate into a floating-point target.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a real literal thrown out on the token's
    /// text, an operand that is already floating point, a division that stays integral, and one that is
    /// already promoted by hand.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public double Half(int value) => value / 2.0;

               public double Ratio(double numerator, int count) => numerator / count;

               public double Promoted(int total, int count) => (double)total / count;

               public int Pages(int total, int size) => total / size;

               public long Chunks(long total, long size) => total / size;

               public decimal Money(decimal amount, int months) => amount / months;
           }
           """;

    /// <summary>Builds one type whose divisions all truncate before they are widened.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public double Average(int total, int count) => total / count;

               public double Stored(int hits, int misses)
               {
                   double ratio = hits / misses;
                   return ratio;
               }

               public float Fraction(int part, int whole) => (float)(part / whole);

               public decimal Rate(int amount, int months) => amount / months;

               public double Root(int a, int b) => System.Math.Sqrt(a / b);
           }
           """;
}
