// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for readonly struct-member analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ReadonlyStructMemberBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic struct count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Members { get; set; }

    /// <summary>Builds the benchmark state.</summary>
    [GlobalSetup]
    public void Setup() => _state = Cases.Create(Members);

    /// <summary>Benchmarks struct members already marked readonly.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> ReadonlyStructMember_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks non-mutating struct members that are not marked readonly.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> ReadonlyStructMember_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Builds benchmark source for readonly struct-member analysis.</summary>
    private static class Source
    {
        /// <summary>Builds clean or reportable struct-member source.</summary>
        /// <param name="members">The number of synthetic structs to emit.</param>
        /// <param name="violating">Whether to emit members that can be marked readonly.</param>
        /// <returns>The generated source text.</returns>
        public static string Generate(int members, bool violating)
            => $$"""
               namespace Bench;

               {{BenchmarkSourceText.JoinBlocks(members, i => GenerateStruct(i, violating))}}
               """;

        /// <summary>Builds one struct declaration.</summary>
        /// <param name="index">The synthetic struct index.</param>
        /// <param name="violating">Whether to emit the reportable member form.</param>
        /// <returns>The generated struct block.</returns>
        private static string GenerateStruct(int index, bool violating)
        {
            var modifier = violating ? string.Empty : "readonly ";
            return $$"""
               internal struct S{{index}}
               {
                   private readonly int _value;

                   public S{{index}}(int value) => _value = value;

                   public {{modifier}}int Value() => _value;
               }
               """;
        }
    }

    /// <summary>Builds benchmark state for readonly struct-member analysis.</summary>
    private static class Cases
    {
        /// <summary>Creates the prepared benchmark state for the requested struct count.</summary>
        /// <param name="members">The synthetic struct count.</param>
        /// <returns>The prepared benchmark state.</returns>
        public static SingleAnalyzerBenchmarkState Create(int members)
            => SingleAnalyzerBenchmarkCases.Create(new Sst1460ReadonlyStructMemberAnalyzer(), Source.Generate, members);
    }
}
