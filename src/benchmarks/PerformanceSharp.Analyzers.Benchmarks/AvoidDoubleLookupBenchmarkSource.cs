// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for avoid-double-lookup analyzer benchmarks.</summary>
internal static class AvoidDoubleLookupBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating membership guard patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit avoid-double-lookup rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating set-mutation type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean set-mutation type whose guard is not a membership check.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly System.Collections.Generic.HashSet<string> _items = new();

               public void M(string item)
               {
                   if (_items.Count > 0)
                   {
                       _items.Remove(item);
                   }
               }
           }
           """;

    /// <summary>Builds one violating set-mutation type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly System.Collections.Generic.HashSet<string> _items = new();

               public void M(string item)
               {
                   if (_items.Contains(item))
                   {
                       _items.Remove(item);
                   }
               }
           }
           """;
}
