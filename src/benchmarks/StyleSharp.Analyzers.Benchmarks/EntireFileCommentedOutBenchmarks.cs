// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Profiles SST1108 startup, live code, prose and commented declarations.</summary>
[System.Diagnostics.DebuggerDisplay("EntireFileCommentedOutBenchmarks: {Path}, {Nodes}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class EntireFileCommentedOutBenchmarks
{
    /// <summary>The prepared analyzer and compilation.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the source size.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Gets or sets the source shape.</summary>
    [Params("startup", "clean", "prose", "line-comment", "block-comment", "incomplete")]
    public string Path { get; set; } = "startup";

    /// <summary>Builds the source and checks its diagnostic count.</summary>
    /// <returns>A task representing setup.</returns>
    /// <exception cref="InvalidOperationException">The input does not exercise the expected rule path.</exception>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        var source = EntireFileCommentedOutBenchmarkSource.Generate(Nodes, Path);
        var scenario = new AnalyzerBenchmarkScenario(BenchmarkCompilationFactory.CreateCompilation(source).Compilation);
        _state = SingleAnalyzerBenchmarkHelper.Create(new Sst1108EntireFileCommentedOutAnalyzer(), scenario, scenario);
        var expected = Path is "line-comment" or "block-comment" ? 1 : 0;
        var actual = await SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state).ConfigureAwait(false);
        if (actual != expected)
        {
            throw new InvalidOperationException($"Expected {expected} diagnostics for {Path}, received {actual}.");
        }
    }

    /// <summary>Analyzes the selected source.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Analyze() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);
}
