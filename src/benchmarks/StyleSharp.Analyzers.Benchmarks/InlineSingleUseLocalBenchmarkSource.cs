// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for single-use-local inlining analyzer benchmarks.</summary>
internal static class InlineSingleUseLocalBenchmarkSource
{
    /// <summary>How often the clean corpus emits a near-miss member rather than unrelated code.</summary>
    private const int NearMissInterval = 4;

    /// <summary>Builds a compilation unit that exercises clean or violating single-use locals.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit locals the rule would inline.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class InlineSingleUseLocalBench
           {
               private readonly int _value = 1;

               private System.Collections.Generic.List<int> Items => new();

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a local the rule would inline.</param>
    /// <returns>The generated member block.</returns>
    /// <remarks>
    /// The clean corpus is mostly code the rule turns back early, because rejecting declarations is where
    /// an analyzer spends its time. One member in <see cref="NearMissInterval"/> is still a near-miss that
    /// reaches the loop check, so the full gate stays covered.
    /// </remarks>
    private static string GenerateMember(int index, bool violating)
    {
        if (violating)
        {
            return GenerateViolatingMember(index);
        }

        return index % NearMissInterval == 0 ? GenerateCleanMember(index) : GenerateUnrelatedMember(index);
    }

    /// <summary>Builds one member whose locals are read more than once, so none is a candidate.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateUnrelatedMember(int index)
        => $$"""
           internal int M{{index}}(int[] values)
           {
               var total = _value;
               var scale = total + 1;
               return (total * scale) + (scale - total);
           }
           """;

    /// <summary>Builds one member whose single-use local the rule must keep.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    /// <remarks>
    /// The read sits inside a loop and the initializer is a property chain, so the clean corpus exercises
    /// the whole gate — the declaration still passes the shape, purity, width and single-reference tests,
    /// and only the loop check turns it back. That keeps both corpora on the same path rather than making
    /// the clean one an early exit. A leaf initializer would not do: re-reading one costs nothing, so the
    /// rule still inlines it into a loop and the corpus would not be clean.
    /// </remarks>
    private static string GenerateCleanMember(int index)
        => $$"""
           internal int M{{index}}(int[] values)
           {
               var total = 0;
               var count = Items.Count;
               foreach (var value in values)
               {
                   total += count;
               }

               return total;
           }
           """;

    /// <summary>Builds one member with a single-use local the rule would inline.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           internal int M{{index}}()
           {
               var local = _value;
               return local;
           }
           """;
}
