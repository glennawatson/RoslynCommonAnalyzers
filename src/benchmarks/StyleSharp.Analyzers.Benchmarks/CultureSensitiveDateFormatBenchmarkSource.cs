// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for culture-sensitive date-format analyzer benchmarks (SST2445).</summary>
internal static class CultureSensitiveDateFormatBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating date/time formats.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Globalization;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose formats are culture-safe.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string Wire(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

               public string Display(DateTime d) => d.ToString("D", CultureInfo.CurrentCulture);
           }
           """;

    /// <summary>Builds one type whose formats lean on the culture's separators.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public string Date(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

               public string Time(DateTime d) => $"{d:HH:mm:ss}";
           }
           """;
}
