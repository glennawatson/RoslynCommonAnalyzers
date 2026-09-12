// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Builds synthetic source for the redundant-conversion benchmarks. The clean corpus is the one that
/// matters: it is full of the shapes the rule registers on — casts, <c>as</c> tests and sequence calls —
/// that it must reject, so it measures the cost paid by every file that has nothing wrong with it.
/// </summary>
internal static class RedundantCastBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or redundant conversions.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit redundant conversions.</param>
    /// <returns>The generated source text.</returns>
    internal static string Generate(int members, bool violating) =>
        $$"""
           #nullable enable
           namespace Bench;

           using System.Collections.Generic;
           using System.Linq;

           internal class RedundantCastBenchBase
           {
           }

           internal sealed class RedundantCastBenchDerived : RedundantCastBenchBase
           {
           }

           internal static class RedundantCastBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one member covering each registered conversion shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether the conversions should be redundant.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating) =>
        violating
            ? $$"""
                    internal static int Cast{{index}}(int value) => (int)value;

                    internal static RedundantCastBenchBase? As{{index}}(RedundantCastBenchBase node) => node as RedundantCastBenchBase;

                    internal static IEnumerable<int> Sequence{{index}}(IEnumerable<int> items) => items.Cast<int>();
                """
            : $$"""
                    internal static long Cast{{index}}(int value) => (long)value;

                    internal static RedundantCastBenchDerived? As{{index}}(RedundantCastBenchBase node) => node as RedundantCastBenchDerived;

                    internal static IEnumerable<string> Sequence{{index}}(IEnumerable<object> items) => items.Cast<string>();

                    internal static int Call{{index}}(IEnumerable<int> items) => items.Count();
                """;
}
