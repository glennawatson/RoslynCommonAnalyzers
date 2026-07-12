// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for overwritten-collection-element analyzer benchmarks.</summary>
internal static class OverwrittenCollectionElementBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating element-overwrite patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit overwritten-collection-element rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Collections.Generic;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating element-overwrite type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose adjacent element writes are all legitimate.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers the rejection routes the no-diagnostic path takes: a statement that is not an element write at
    /// all, two writes whose indexes differ, a compound assignment, a right-hand side that reads the element,
    /// and an index that cannot be evaluated twice.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private int _cursor;

               public void Fill(int[] values, int i)
               {
                   values[i] = 1;
                   values[i + 1] = 2;
                   var total = values[i] + values[i + 1];
                   values[i] = total;
               }

               public void Accumulate(int[] sums, int i, int x, int y)
               {
                   sums[i] += x;
                   sums[i] += y;
               }

               public void Bump(int[] values, int i)
               {
                   values[i] = 1;
                   values[i] = values[i] + 1;
               }

               public void Advance(Dictionary<int, int> map)
               {
                   map[Next()] = 1;
                   map[Next()] = 2;
               }

               private int Next() => _cursor++;
           }
           """;

    /// <summary>Builds one type with two lost writes.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Fill(int[] values, int i)
               {
                   values[i] = ComputeX();
                   values[i] = ComputeY();
               }

               public void Map(Dictionary<string, int> map)
               {
                   map["alpha"] = 1;
                   map["alpha"] = 2;
               }

               private static int ComputeX() => 1;

               private static int ComputeY() => 2;
           }
           """;
}
