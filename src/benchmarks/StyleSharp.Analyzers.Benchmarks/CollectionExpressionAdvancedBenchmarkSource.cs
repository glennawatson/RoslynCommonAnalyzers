// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for advanced collection-expression analysis.</summary>
internal static class CollectionExpressionAdvancedBenchmarkSource
{
    /// <summary>The number of shapes cycled by the analyzer benchmark corpus.</summary>
    private const int ShapeCount = 4;

    /// <summary>Builds clean or violating source for the advanced collection-expression analyzer.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => GenerateCore(members, violating, shape: null);

    /// <summary>Builds source containing one repeated shape for code-fix benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="shape">The repeated shape.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCodeFix(int members, CollectionExpressionAdvancedBenchmarkShape shape)
        => GenerateCore(members, violating: true, shape);

    /// <summary>Builds the benchmark source body.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <param name="shape">The fixed shape, or <see langword="null"/> to cycle all shapes.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateCore(int members, bool violating, CollectionExpressionAdvancedBenchmarkShape? shape)
        => $$"""
           using System;
           using System.Collections;
           using System.Collections.Generic;
           using System.Linq;
           using System.Runtime.CompilerServices;

           namespace Bench;

           [CollectionBuilder(typeof(MyCollection), "Create")]
           internal sealed class MyCollection<T> : IEnumerable<T>
           {
               public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();

               IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
           }

           internal static class MyCollection
           {
               public static MyCollection<T> Create<T>(params T[] values) => throw new NotImplementedException();

               public static MyCollection<T> Create<T>(ReadOnlySpan<T> values) => throw new NotImplementedException();

               public static MyCollectionBuilder<T> CreateBuilder<T>() => new();
           }

           internal sealed class MyCollectionBuilder<T>
           {
               public void Add(T value) { }

               public MyCollection<T> ToImmutable() => throw new NotImplementedException();
           }

           internal static class CollectionExpressionAdvancedBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating, shape ?? (CollectionExpressionAdvancedBenchmarkShape)(i % ShapeCount)))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating, CollectionExpressionAdvancedBenchmarkShape shape)
        => (shape, violating) switch
        {
            (CollectionExpressionAdvancedBenchmarkShape.Stackalloc, true) => $$"""
                                                                               internal static int Stackalloc{{index}}()
                                                                               {
                                                                                   Span<int> values = stackalloc int[] { {{index}}, {{index + 1}} };
                                                                                   return values[0] + values[1];
                                                                               }
                                                                               """,
            (CollectionExpressionAdvancedBenchmarkShape.Stackalloc, false) => $$"""
                                                                                internal static int Stackalloc{{index}}()
                                                                                {
                                                                                    Span<int> values = [{{index}}, {{index + 1}}];
                                                                                    return values[0] + values[1];
                                                                                }
                                                                                """,
            (CollectionExpressionAdvancedBenchmarkShape.Create, true) => $$"""
                                                                           internal static MyCollection<int> Create{{index}}()
                                                                               => MyCollection.Create({{index}}, {{index + 1}});
                                                                           """,
            (CollectionExpressionAdvancedBenchmarkShape.Create, false) => $$"""
                                                                            internal static MyCollection<int> Create{{index}}()
                                                                                => [{{index}}, {{index + 1}}];
                                                                            """,
            (CollectionExpressionAdvancedBenchmarkShape.Builder, true) => $$"""
                                                                            internal static MyCollection<int> Builder{{index}}()
                                                                            {
                                                                                var builder = MyCollection.CreateBuilder<int>();
                                                                                builder.Add({{index}});
                                                                                builder.Add({{index + 1}});
                                                                                return builder.ToImmutable();
                                                                            }
                                                                            """,
            (CollectionExpressionAdvancedBenchmarkShape.Builder, false) => $$"""
                                                                             internal static MyCollection<int> Builder{{index}}()
                                                                                 => [{{index}}, {{index + 1}}];
                                                                             """,
            (CollectionExpressionAdvancedBenchmarkShape.Fluent, true) => $$"""
                                                                           internal static List<int> Fluent{{index}}()
                                                                               => new[] { {{index}}, {{index + 1}} }.ToList();
                                                                           """,
            _ => $$"""
                   internal static List<int> Fluent{{index}}()
                       => [{{index}}, {{index + 1}}];
                   """
        };
}
