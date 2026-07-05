// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for unused private-parameter analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class UnusedPrivateParameterBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic method count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Members { get; set; }

    /// <summary>Builds the benchmark state.</summary>
    [GlobalSetup]
    public void Setup() => _state = Cases.Create(Members);

    /// <summary>Benchmarks private parameters that are read.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> UnusedPrivateParameter_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks private parameters that are never read.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> UnusedPrivateParameter_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Builds benchmark source for unused private-parameter analysis.</summary>
    private static class Source
    {
        /// <summary>Builds clean or reportable private-parameter source.</summary>
        /// <param name="members">The number of synthetic methods to emit.</param>
        /// <param name="violating">Whether to emit unread parameters.</param>
        /// <returns>The generated source text.</returns>
        public static string Generate(int members, bool violating)
            => $$"""
               namespace Bench;

               internal sealed class C
               {
               {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMethod(i, violating))}}
               }
               """;

        /// <summary>Builds one private method.</summary>
        /// <param name="index">The synthetic method index.</param>
        /// <param name="violating">Whether to emit the unread-parameter form.</param>
        /// <returns>The generated method declaration.</returns>
        private static string GenerateMethod(int index, bool violating)
            => violating
                ? $"    private int M{index}(int value{index}) => {index};"
                : $"    private int M{index}(int value{index}) => value{index};";
    }

    /// <summary>Builds benchmark state for unused private-parameter analysis.</summary>
    private static class Cases
    {
        /// <summary>Creates the prepared benchmark state for the requested method count.</summary>
        /// <param name="members">The synthetic method count.</param>
        /// <returns>The prepared benchmark state.</returns>
        public static SingleAnalyzerBenchmarkState Create(int members)
            => SingleAnalyzerBenchmarkCases.Create(new Sst1461UnusedParameterAnalyzer(), Source.Generate, members);
    }
}
