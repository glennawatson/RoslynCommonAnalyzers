// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Builds synthetic source for meaningless count-comparison analysis. The clean corpus produces no
/// diagnostics; the violating corpus produces exactly six per type, so a run of N types reports 6N.
/// </summary>
internal static class MeaninglessCountComparisonBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating count comparisons.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit count-comparison rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Collections.Generic;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating count-comparison type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose comparisons all still ask a real question.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a comparison the fold leaves alone, a
    /// positive bound, and two operands that fold but are not counts, which must die at the name gate rather
    /// than at the semantic model.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly List<int> _items = new List<int>();

               private readonly int[] _values = new int[4];

               public bool Any() => _items.Count > 0;

               public bool Empty() => _items.Count == 0;

               public bool Pair() => _values.Length == 2;

               public bool Long(string text) => text.Length > 8;

               public bool Found(string text) => text.IndexOf('a') >= 0;

               public bool NonNegative(int value) => value >= 0;
           }
           """;

    /// <summary>Builds one type whose comparisons are all decided before they run.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private readonly List<int> _items = new List<int>();

               private readonly int[] _values = new int[4];

               public bool Any() => _items.Count >= 0;

               public bool Broken() => _items.Count < 0;

               public bool Missing() => _values.Length == -1;

               public bool Present(string text) => text.Length != -1;

               public bool Reversed() => 0 <= _values.Length;

               public bool AtLeast(string text) => text.Length > -1;
           }
           """;
}
