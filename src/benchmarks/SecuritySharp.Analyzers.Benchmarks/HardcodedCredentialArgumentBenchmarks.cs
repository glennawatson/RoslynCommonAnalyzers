// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Measures the clean and violating credential argument paths.</summary>
[System.Diagnostics.DebuggerDisplay("HardcodedCredentialArgumentBenchmarks: {Nodes}")]
[ShortRunJob]
[MemoryDiagnoser]
public class HardcodedCredentialArgumentBenchmarks
{
    /// <summary>The prepared analyzer and compilations.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the number of generated types.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds both scenarios before measurement.</summary>
    [GlobalSetup]
    public void Setup() => _state = HardcodedCredentialArgumentBenchmarkCases.Create(Nodes);

    /// <summary>Measures variable credentials, placeholders, and ordinary logging.</summary>
    /// <returns>The diagnostic count, which must be zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> HardcodedCredentialArgument_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Measures reportable literals alongside clean calls.</summary>
    /// <returns>The diagnostic count, which must match the generated type count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Benchmark]
    public Task<int> HardcodedCredentialArgument_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
