// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for enum switch-statement mapping analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class EnumSwitchStatementMappingBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic switch count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Members { get; set; }

    /// <summary>Builds the benchmark state.</summary>
    [GlobalSetup]
    public void Setup() => _state = Cases.Create(Members);

    /// <summary>Benchmarks enum switch-statement mappings that name every value.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> EnumSwitchStatementMapping_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks enum switch-statement mappings that omit values.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> EnumSwitchStatementMapping_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Builds benchmark source for enum switch-statement mapping analysis.</summary>
    private static class Source
    {
        /// <summary>Builds clean or reportable enum switch-statement mappings.</summary>
        /// <param name="members">The number of synthetic switch statements to emit.</param>
        /// <param name="violating">Whether to omit one enum value from each mapping.</param>
        /// <returns>The generated source text.</returns>
        public static string Generate(int members, bool violating)
            => $$"""
               namespace Bench;

               internal enum Color
               {
                   Red,
                   Green,
                   Blue
               }

               internal sealed class C
               {
               {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMethod(i, violating))}}
               }
               """;

        /// <summary>Builds one switch-statement mapping method.</summary>
        /// <param name="index">The synthetic method index.</param>
        /// <param name="violating">Whether to omit an enum value.</param>
        /// <returns>The generated method block.</returns>
        private static string GenerateMethod(int index, bool violating)
        {
            var blueSection = violating
                ? string.Empty
                : """
                          case Color.Blue:
                              return 3;
                  """;

            return $$"""
                   public int Map{{index}}(Color color)
                   {
                       switch (color)
                       {
                           case Color.Red:
                               return 1;
                           case Color.Green:
                               return 2;
               {{blueSection}}
                       }

                       return 0;
                   }
               """;
        }
    }

    /// <summary>Builds benchmark state for enum switch-statement mapping analysis.</summary>
    private static class Cases
    {
        /// <summary>Creates the prepared benchmark state for the requested switch count.</summary>
        /// <param name="members">The synthetic switch count.</param>
        /// <returns>The prepared benchmark state.</returns>
        public static SingleAnalyzerBenchmarkState Create(int members)
            => SingleAnalyzerBenchmarkCases.Create(new Sst2242EnumSwitchStatementMappingAnalyzer(), Source.Generate, members);
    }
}
