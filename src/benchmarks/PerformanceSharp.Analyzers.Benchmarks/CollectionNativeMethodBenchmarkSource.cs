// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for collection native-method analysis.</summary>
internal static class CollectionNativeMethodBenchmarkSource
{
    /// <summary>The number of shapes cycled by the analyzer benchmark corpus.</summary>
    private const int ShapeCount = 3;

    /// <summary>Builds clean or violating source for the collection native-method analyzer.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => GenerateCore(members, violating, shape: null);

    /// <summary>Builds source containing one repeated shape for code-fix benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="shape">The repeated shape.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCodeFix(int members, CollectionNativeMethodBenchmarkShape shape)
        => GenerateCore(members, violating: true, shape);

    /// <summary>Builds the benchmark source body.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <param name="shape">The fixed shape, or <see langword="null"/> to cycle all shapes.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateCore(int members, bool violating, CollectionNativeMethodBenchmarkShape? shape)
        => $$"""
           using System.Collections.Generic;
           using System.Linq;

           namespace Bench;

           internal sealed class CollectionNativeMethodBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating, shape ?? (CollectionNativeMethodBenchmarkShape)(i % ShapeCount)))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating, CollectionNativeMethodBenchmarkShape shape)
        => shape switch
        {
            CollectionNativeMethodBenchmarkShape.ListPredicate => GenerateListPredicate(index, violating),
            CollectionNativeMethodBenchmarkShape.ArrayPredicate => GenerateArrayPredicate(index, violating),
            _ => GenerateMembership(index, violating)
        };

    /// <summary>Builds one List predicate shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateListPredicate(int index, bool violating)
        => violating
            ? $$"""
                private int ListPredicate{{index}}(List<int> values) => values.FirstOrDefault(value => value > {{index}});
                """
            : $$"""
                private int ListPredicate{{index}}(List<int> values) => values.Find(value => value > {{index}});
                """;

    /// <summary>Builds one array predicate shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateArrayPredicate(int index, bool violating)
        => violating
            ? $$"""
                private bool ArrayPredicate{{index}}(int[] values) => values.Any(value => value > {{index}});
                """
            : $$"""
                private bool ArrayPredicate{{index}}(int[] values) => System.Array.Exists(values, value => value > {{index}});
                """;

    /// <summary>Builds one membership-test shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMembership(int index, bool violating)
        => violating
            ? $$"""
                private bool Membership{{index}}(List<int> values, int target) => values.Any(value => value == target);
                """
            : $$"""
                private bool Membership{{index}}(List<int> values, int target) => values.Contains(target);
                """;
}
