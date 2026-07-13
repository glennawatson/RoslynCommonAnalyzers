// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for settable-collection-property analysis (SST2305).</summary>
internal static class CollectionPropertyBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating collection properties.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Collections.Generic;
           using System.Collections.Immutable;
           using System.Collections.ObjectModel;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose collection properties are all exempt.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: no setter at all, an <c>init</c>
    /// setter, a private setter, an attributed property, a read-only interface, a read-only wrapper, an
    /// immutable collection, and a scalar.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public List<int> Items { get; } = new List<int>();

               public List<int> Built { get; init; }

               public List<int> Owned { get; private set; }

               [Obsolete("Use Items.")]
               public List<int> Legacy { get; set; }

               public IReadOnlyList<int> Readable { get; set; }

               public ReadOnlyCollection<int> View { get; set; }

               public ImmutableArray<int> Frozen { get; set; }

               public int Count { get; set; }
           }
           """;

    /// <summary>Builds one type whose collection properties can all be replaced by a caller.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public List<int> Items { get; set; }

               public int[] Values { get; set; }

               public Dictionary<string, int> Map { get; set; }

               public HashSet<int> Tags { get; set; }

               public IList<int> Listed { get; set; }
           }
           """;
}
