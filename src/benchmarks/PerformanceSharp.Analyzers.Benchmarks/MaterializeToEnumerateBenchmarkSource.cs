// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for materialize-to-enumerate analyzer benchmarks.</summary>
internal static class MaterializeToEnumerateBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating materialize-to-enumerate patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Linq;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type whose foreach enumerates a filtered source directly.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int M(int[] values)
               {
                   var total = 0;
                   foreach (var value in values.Where(v => v > 0))
                   {
                       total += value;
                   }

                   return total;
               }
           }
           """;

    /// <summary>Builds one violating type whose foreach materializes the sequence it enumerates.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int M(int[] values)
               {
                   var total = 0;
                   foreach (var value in values.Where(v => v > 0).ToList())
                   {
                       total += value;
                   }

                   return total;
               }
           }
           """;
}
