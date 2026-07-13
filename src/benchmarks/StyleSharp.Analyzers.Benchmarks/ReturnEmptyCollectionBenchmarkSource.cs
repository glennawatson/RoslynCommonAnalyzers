// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for null-returned-for-a-collection analysis (SST2306).</summary>
internal static class ReturnEmptyCollectionBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating collection returns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           #nullable enable
           using System;
           using System.Collections.Generic;
           using System.Threading.Tasks;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose returns are all exempt or non-null.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: an expression with no null in it, a
    /// nullable return type, a string, a scalar, a task, and a lambda whose shape its delegate dictates.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly List<int> _items = new List<int>();

               public IReadOnlyList<int> Items => _items;

               public int[] Values() => new int[1];

               public IEnumerable<int>? Maybe() => null;

               public string? Name() => null;

               public object? State() => null;

               public Task<List<int>>? ItemsAsync() => null;

               public Func<int[]?> Factory() => () => null;
           }
           """;

    /// <summary>Builds one type whose collection members all hand back null.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private readonly List<int> _items = new List<int>();

               public List<int> Items
               {
                   get { return null; }
               }

               public int[] Values()
               {
                   return null;
               }

               public IEnumerable<string> Names() => null;

               public ISet<int> Tags() => null;

               public IReadOnlyList<int> Visible(bool enabled) => enabled ? _items : null;
           }
           """;
}
