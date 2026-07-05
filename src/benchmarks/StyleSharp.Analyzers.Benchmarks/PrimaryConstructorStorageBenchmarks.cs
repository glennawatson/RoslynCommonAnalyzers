// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for primary-constructor storage analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PrimaryConstructorStorageBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark state.</summary>
    [GlobalSetup]
    public void Setup() => _state = Cases.Create(Types);

    /// <summary>Benchmarks constructor storage already expressed on the type declaration.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> PrimaryConstructorStorage_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks constructors that only store parameters.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> PrimaryConstructorStorage_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Builds benchmark source for primary-constructor storage analysis.</summary>
    private static class Source
    {
        /// <summary>Builds clean or reportable constructor-storage source.</summary>
        /// <param name="types">The number of synthetic types to emit.</param>
        /// <param name="violating">Whether to emit constructors that only copy parameters to members.</param>
        /// <returns>The generated source text.</returns>
        public static string Generate(int types, bool violating)
            => $$"""
               namespace Bench;

               {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
               """;

        /// <summary>Builds one type declaration.</summary>
        /// <param name="index">The synthetic type index.</param>
        /// <param name="violating">Whether to emit the reportable constructor form.</param>
        /// <returns>The generated type block.</returns>
        private static string GenerateType(int index, bool violating)
            => violating
                ? $$"""
                  internal sealed class C{{index}}
                  {
                      private readonly int _value;

                      public C{{index}}(int value)
                      {
                          _value = value;
                      }
                  }
                  """
                : $$"""
                  internal sealed class C{{index}}(int value)
                  {
                      private readonly int _value = value;
                  }
                  """;
    }

    /// <summary>Builds benchmark state for primary-constructor storage analysis.</summary>
    private static class Cases
    {
        /// <summary>Creates the prepared benchmark state for the requested type count.</summary>
        /// <param name="types">The synthetic type count.</param>
        /// <returns>The prepared benchmark state.</returns>
        public static SingleAnalyzerBenchmarkState Create(int types)
            => SingleAnalyzerBenchmarkCases.Create(new Sst2241PrimaryConstructorStorageAnalyzer(), Source.Generate, types);
    }
}
