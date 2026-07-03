// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for LINQ usage analysis.</summary>
internal static class LinqUsageBenchmarkSource
{
    /// <summary>The number of shapes cycled by the analyzer benchmark corpus.</summary>
    private const int ShapeCount = 3;

    /// <summary>Builds clean or violating source for the LINQ usage analyzer.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => GenerateCore(members, violating, shape: null);

    /// <summary>Builds source containing one repeated shape for code-fix benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="shape">The repeated shape.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCodeFix(int members, LinqUsageBenchmarkShape shape)
        => GenerateCore(members, violating: true, shape);

    /// <summary>Builds the benchmark source body.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <param name="shape">The fixed shape, or <see langword="null"/> to cycle all shapes.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateCore(int members, bool violating, LinqUsageBenchmarkShape? shape)
        => $$"""
           using System;
           using System.Linq;

           namespace Bench;

           internal sealed class LinqUsageBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating, shape ?? (LinqUsageBenchmarkShape)(i % ShapeCount)))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating, LinqUsageBenchmarkShape shape)
        => shape switch
        {
            LinqUsageBenchmarkShape.WhereTerminal => GenerateWhereTerminal(index, violating),
            LinqUsageBenchmarkShape.TypeFilter => GenerateTypeFilter(index, violating),
            _ => GenerateHotPathLinq(index, violating)
        };

    /// <summary>Builds one LINQ Where-terminal shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateWhereTerminal(int index, bool violating)
        => violating
            ? $$"""
                private bool WhereTerminal{{index}}(int[] values) => values.Where(value => value > {{index}}).Any();
                """
            : $$"""
                private bool WhereTerminal{{index}}(int[] values) => values.Any(value => value > {{index}});
                """;

    /// <summary>Builds one LINQ type-filter shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateTypeFilter(int index, bool violating)
        => violating
            ? $$"""
                private object TypeFilter{{index}}(object[] values) => values.Where(value => value is string).Cast<string>();
                """
            : $$"""
                private object TypeFilter{{index}}(object[] values) => values.OfType<string>();
                """;

    /// <summary>Builds one hot-path LINQ shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateHotPathLinq(int index, bool violating)
        => violating
            ? $$"""
                private object HotPathLinq{{index}}(int[] values) => values.Select(value => value + {{index}});
                """
            : $$"""
                private int HotPathLinq{{index}}(int[] values)
                {
                    var sum = 0;
                    for (var i = 0; i < values.Length; i++)
                    {
                        sum += values[i] + {{index}};
                    }

                    return sum;
                }
                """;
}
