// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Profiles SES1005 startup, ordinary expectation comparisons, and secret comparisons.</summary>
[System.Diagnostics.DebuggerDisplay("SecretComparisonProfiledAllocBenchmarks: {Types}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class SecretComparisonProfiledAllocBenchmarks
{
    /// <summary>The analyzer and prepared clean and violating compilations.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>The empty compilation that isolates fixed registration costs.</summary>
    private AnalyzerBenchmarkScenario _startup;

    /// <summary>Gets or sets the number of generated comparison types.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Prepares compilable corpora and checks every expected diagnostic count.</summary>
    /// <returns>A task representing the asynchronous setup.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _state = SecretComparisonBenchmarkCases.Create(Types);
        _startup = SecretComparisonBenchmarkCases.CreateStartup();
        await SecretComparisonBenchmarkCases.ValidateAsync(_startup, _state.Analyzers, expectedCount: 0).ConfigureAwait(false);
        await SecretComparisonBenchmarkCases.ValidateAsync(_state.CleanScenario, _state.Analyzers, expectedCount: 0).ConfigureAwait(false);
        await SecretComparisonBenchmarkCases.ValidateAsync(_state.ViolatingScenario, _state.Analyzers, Types * SecretComparisonBenchmarkSource.ComparisonsPerType).ConfigureAwait(false);
    }

    /// <summary>Measures fixed analyzer registration on an empty type.</summary>
    /// <returns>The diagnostic count, which must be zero.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Startup() => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(_startup, _state.Analyzers);

    /// <summary>Measures ordinary expected/actual comparisons across operators, methods, and byte buffers.</summary>
    /// <returns>The diagnostic count, which must be zero.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Measures explicit secret names in the corresponding comparison shapes.</summary>
    /// <returns>The diagnostic count expected for the generated corpus.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
