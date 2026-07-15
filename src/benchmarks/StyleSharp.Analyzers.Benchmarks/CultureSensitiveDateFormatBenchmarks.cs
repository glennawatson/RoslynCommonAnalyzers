// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for culture-sensitive date-format analysis (SST2445).</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CultureSensitiveDateFormatBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup() => _state = SingleAnalyzerBenchmarkCases.Create(new Sst2445CultureSensitiveDateFormatAnalyzer(), CultureSensitiveDateFormatBenchmarkSource.Generate, Nodes);

    /// <summary>Benchmarks the clean culture-sensitive date-format path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> CultureSensitiveDateFormat_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating culture-sensitive date-format path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> CultureSensitiveDateFormat_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
