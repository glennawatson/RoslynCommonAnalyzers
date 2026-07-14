// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for enum-naming analyzer benchmarks (SST1319).</summary>
internal static class EnumNamingBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating enum names.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one block of enums whose names are all PascalCase.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated block.</returns>
    /// <remarks>
    /// Covers the routes the character scan must walk without reporting: a plain name, a two-letter
    /// acronym that stays upper, a trailing two-letter acronym, and a digit — the longest clean path
    /// through the scan, since every character has to be read before the name is accepted.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public enum LogLevel{{index}} { Debug, Info, Warning }

           public enum IOMode{{index}} { Read, Write }

           public enum Http2Client{{index}} { Get, Post }

           public sealed class C{{index}}
           {
               public LogLevel{{index}} Level { get; set; }
           }
           """;

    /// <summary>Builds one block of enums whose names are not PascalCase.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public enum Log_Level{{index}} { Debug, Info, Warning }

           public enum HTTPStatus{{index}} { Ok, NotFound }

           public enum MyENUM{{index}} { First, Second }

           public sealed class V{{index}}
           {
               public Log_Level{{index}} Level { get; set; }
           }
           """;
}
