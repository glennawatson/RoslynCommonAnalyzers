// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for SST1142 tuple-element benchmarks.</summary>
internal static class TupleElementNameBenchmarkSource
{
    /// <summary>Builds a compilation unit containing tuple element accesses.</summary>
    /// <param name="nodes">The number of member-access nodes to emit.</param>
    /// <param name="violating">Whether to emit <c>ItemN</c> accesses instead of named accesses.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int nodes, bool violating)
    {
        var memberName = violating ? "Item1" : "first";
        return $$"""
                namespace Bench;
                public static class TupleBench
                {
                    public static void Visit((int first, int second) value)
                    {
                {{BenchmarkSourceText.JoinLines(nodes, _ => $"        Sink(value.{memberName});")}}
                    }

                    private static void Sink(int value) { }
                }
                """;
    }
}
