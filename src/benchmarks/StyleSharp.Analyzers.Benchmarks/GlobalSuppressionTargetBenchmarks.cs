// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for global-suppression target validation.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class GlobalSuppressionTargetBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Members { get; set; }

    /// <summary>Builds the benchmark state.</summary>
    [GlobalSetup]
    public void Setup() => _state = Cases.Create(Members);

    /// <summary>Benchmarks resolvable global-suppression targets.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> GlobalSuppressionTarget_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks unresolved and legacy global-suppression targets.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> GlobalSuppressionTarget_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Builds benchmark source for global-suppression target validation.</summary>
    private static class Source
    {
        /// <summary>Builds clean or reportable global-suppression targets.</summary>
        /// <param name="members">The number of synthetic members and suppressions to emit.</param>
        /// <param name="violating">Whether to emit unresolved or legacy target strings.</param>
        /// <returns>The generated source text.</returns>
        public static string Generate(int members, bool violating)
            => $$"""
               using System.Diagnostics.CodeAnalysis;

               {{BenchmarkSourceText.JoinLines(members, i => GenerateSuppression(i, violating))}}

               namespace Bench;

               internal sealed class C
               {
               {{BenchmarkSourceText.JoinBlocks(members, GenerateMember)}}
               }
               """;

        /// <summary>Builds one assembly suppression.</summary>
        /// <param name="index">The synthetic member index.</param>
        /// <param name="violating">Whether the suppression should be reportable.</param>
        /// <returns>The generated suppression attribute.</returns>
        private static string GenerateSuppression(int index, bool violating)
        {
            var target = violating && (index & 1) == 0
                ? $"M:Bench.C.Missing{index}"
                : $"M:Bench.C.M{index}";

            if (violating && (index & 1) == 1)
            {
                target = "~" + target;
            }

            return $"""[assembly: SuppressMessage("Style", "SST1000", Justification = "Benchmark.", Scope = "member", Target = "{target}")]""";
        }

        /// <summary>Builds one target member.</summary>
        /// <param name="index">The synthetic member index.</param>
        /// <returns>The generated member declaration.</returns>
        private static string GenerateMember(int index) => $"    public void M{index}() {{ }}";
    }

    /// <summary>Builds benchmark state for global-suppression target validation.</summary>
    private static class Cases
    {
        /// <summary>Creates the prepared benchmark state for the requested member count.</summary>
        /// <param name="members">The synthetic member count.</param>
        /// <returns>The prepared benchmark state.</returns>
        public static SingleAnalyzerBenchmarkState Create(int members)
            => SingleAnalyzerBenchmarkCases.Create(new GlobalSuppressionTargetAnalyzer(), Source.Generate, members);
    }
}
