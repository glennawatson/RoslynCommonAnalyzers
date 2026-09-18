// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Supplies unchanged loop operands, advancing counters and collection snapshots.</summary>
internal static class RuleInteractionBenchmarkSource
{
    /// <summary>Builds a complete application corpus.</summary>
    /// <param name="rule">The diagnostic identifier.</param>
    /// <param name="nodes">The number of application types.</param>
    /// <param name="violating">Whether each type should report.</param>
    /// <returns>The generated source.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Generate(string rule, int nodes, bool violating) =>
        $"using System.Collections.Generic; using System.Linq;\n{BenchmarkSourceText.JoinBlocks(nodes, index => GenerateType(rule, index, violating))}";

    /// <summary>Builds one independently compilable example.</summary>
    /// <param name="rule">The diagnostic identifier.</param>
    /// <param name="index">The unique type index.</param>
    /// <param name="violating">Whether the type should report.</param>
    /// <returns>The declaration source.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule has no corpus.</exception>
    private static string GenerateType(string rule, int index, bool violating) => rule switch
    {
        "PSH1402" => $$"""
            public class Retry{{index}}
            {
                public void Run(int attempt, System.Func<bool> stop)
                {
                    var {{(violating ? "limit = 5" : "retries = 0")}};
                    do
                    {
                        if (stop()) break;
                        attempt++;
                    }
                    while ({{(violating ? "attempt < limit" : "retries < 5")}});
                }
            }
            """,
        "PSH1017" => $$"""
            public class Catalog{{index}}
            {
                private static readonly int[] Names = { 1, 2 };
                public List<int> Items {{(violating ? "=>" : "{ get; } =")}} Names.Select(static value => value * 2).ToList();
            }
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };
}
