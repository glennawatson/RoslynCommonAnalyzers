// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for non-flags-enum-bitwise analyzer benchmarks (SST2458).</summary>
internal static class NonFlagsEnumBitwiseBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating bitwise operations.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one block whose bitwise work is all legitimate.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: operations on a flags enum (one
    /// bind plus the attribute scan), operations on integers (one bind, which settles both
    /// operands), literal and numeric-cast operands (no bind at all), and equality comparisons on a
    /// non-flags enum (never registered).
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           [System.Flags]
           public enum Options{{index}}
           {
               None = 0,
               Cache = 1,
               Retry = 2,
               Log = 4,
           }

           public enum Level{{index}}
           {
               Low,
               Mid,
               High,
           }

           public static class C{{index}}
           {
               public static Options{{index}} Merge(Options{{index}} left, Options{{index}} right)
               {
                   var merged = left | right;
                   merged &= ~Options{{index}}.Cache;
                   return merged ^ Options{{index}}.Retry;
               }

               public static int MaskBits(int value)
               {
                   var low = value & 0xFF;
                   low |= 0x100;
                   return low ^ ~value;
               }

               public static int Pack(Level{{index}} level) => (int)level | 0x40;

               public static bool IsHigh(Level{{index}} level) => level == Level{{index}}.High;
           }
           """;

    /// <summary>Builds one block that combines a non-flags enum's values with bitwise operators.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Each block carries four violations: an or, a mask whose complement folds into the outermost
    /// report, a compound or-assignment, and a xor.
    /// </remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public enum Mode{{index}}
           {
               Plain,
               Debug,
               Verbose,
           }

           public static class V{{index}}
           {
               public static Mode{{index}} Combine(Mode{{index}} left, Mode{{index}} right)
               {
                   var merged = left | right;
                   var masked = merged & ~Mode{{index}}.Debug;
                   masked |= Mode{{index}}.Verbose;
                   return masked ^ left;
               }
           }
           """;
}
