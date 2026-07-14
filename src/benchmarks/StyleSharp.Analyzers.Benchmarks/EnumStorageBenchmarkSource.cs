// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for enum-storage analysis (SST2313).</summary>
internal static class EnumStorageBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises allowed or disallowed enum storage.</summary>
    /// <param name="types">The number of synthetic enums to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    /// <remarks>
    /// No analyzer-config options are supplied, so the rule runs on its default allowed list of <c>int</c>
    /// alone — which is what the violating corpus is measured against.
    /// </remarks>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateEnum(i, violating))}}
           """;

    /// <summary>Builds one clean or violating enum.</summary>
    /// <param name="index">The synthetic enum index.</param>
    /// <param name="violating">Whether to emit a violating enum.</param>
    /// <returns>The generated enum block.</returns>
    private static string GenerateEnum(int index, bool violating)
        => violating ? GenerateViolatingEnum(index) : GenerateCleanEnum(index);

    /// <summary>Builds one enum stored as an int, with and without saying so.</summary>
    /// <param name="index">The synthetic enum index.</param>
    /// <returns>The generated enum block.</returns>
    /// <remarks>
    /// Most enums name no underlying type, and those are the ones the clean path is built for: a null check
    /// on the base list rejects them without a bind or an option read. The explicit <c>: int</c> is the case
    /// that does pay for both, so the corpus carries some of each.
    /// </remarks>
    private static string GenerateCleanEnum(int index)
        => $$"""
           public enum Level{{index}}
           {
               Low = 0,
               High = 1,
           }

           public enum Grade{{index}} : int
           {
               Low = 0,
               High = 1,
           }

           [Flags]
           public enum Options{{index}}
           {
               None = 0,
               First = 1,
               Second = 2,
           }
           """;

    /// <summary>Builds one enum stored as something other than an int.</summary>
    /// <param name="index">The synthetic enum index.</param>
    /// <returns>The generated enum block.</returns>
    private static string GenerateViolatingEnum(int index)
        => $$"""
           public enum Packed{{index}} : byte
           {
               Low = 0,
               High = 1,
           }

           public enum Wide{{index}} : long
           {
               Low = 0,
               High = 1,
           }

           [Flags]
           public enum Narrow{{index}} : ushort
           {
               None = 0,
               First = 1,
               Second = 2,
           }
           """;
}
