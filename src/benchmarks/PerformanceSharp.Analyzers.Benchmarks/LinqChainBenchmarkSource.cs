// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for LINQ chain analysis.</summary>
internal static class LinqChainBenchmarkSource
{
    /// <summary>The number of shapes cycled by the analyzer benchmark corpus.</summary>
    private const int ShapeCount = 3;

    /// <summary>Builds clean or violating source for the LINQ chain analyzer.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => GenerateCore(members, violating, shape: null);

    /// <summary>Builds source containing one repeated shape for code-fix benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="shape">The repeated shape.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCodeFix(int members, LinqChainBenchmarkShape shape)
        => GenerateCore(members, violating: true, shape);

    /// <summary>Builds the benchmark source body.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <param name="shape">The fixed shape, or <see langword="null"/> to cycle all shapes.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateCore(int members, bool violating, LinqChainBenchmarkShape? shape)
        => $$"""
           using System;
           using System.Linq;

           namespace Bench;

           internal sealed class LinqChainBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating, shape ?? (LinqChainBenchmarkShape)(i % ShapeCount)))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating, LinqChainBenchmarkShape shape)
        => shape switch
        {
            LinqChainBenchmarkShape.FilterBeforeSort => GenerateFilterBeforeSort(index, violating),
            LinqChainBenchmarkShape.UseThenBy => GenerateUseThenBy(index, violating),
            _ => GenerateMergeConsecutiveWhere(index, violating)
        };

    /// <summary>Builds one filter-after-sort shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateFilterBeforeSort(int index, bool violating)
        => violating
            ? $$"""
                private object FilterBeforeSort{{index}}(int[] values) => values.OrderBy(value => value).Where(value => value > {{index}});
                """
            : $$"""
                private object FilterBeforeSort{{index}}(int[] values) => values.Where(value => value > {{index}}).OrderBy(value => value);
                """;

    /// <summary>Builds one repeated-sort shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateUseThenBy(int index, bool violating)
        => violating
            ? $$"""
                private object UseThenBy{{index}}(int[] values) => values.OrderBy(value => value % 2).OrderBy(value => value + {{index}});
                """
            : $$"""
                private object UseThenBy{{index}}(int[] values) => values.OrderBy(value => value % 2).ThenBy(value => value + {{index}});
                """;

    /// <summary>Builds one consecutive-Where shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMergeConsecutiveWhere(int index, bool violating)
        => violating
            ? $$"""
                private object MergeConsecutiveWhere{{index}}(int[] values) => values.Where(value => value > 0).Where(value => value < {{index}});
                """
            : $$"""
                private object MergeConsecutiveWhere{{index}}(int[] values) => values.Where(value => value > 0 && value < {{index}});
                """;
}
