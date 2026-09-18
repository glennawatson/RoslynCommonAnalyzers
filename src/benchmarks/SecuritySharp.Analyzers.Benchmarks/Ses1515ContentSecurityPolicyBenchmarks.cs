// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Measures SES1515 startup, clean policy analysis and diagnostic production with allocation traces.</summary>
[System.Diagnostics.DebuggerDisplay("Ses1515ContentSecurityPolicyBenchmarks: {Nodes}")]
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class Ses1515ContentSecurityPolicyBenchmarks
{
    /// <summary>The analyzer and prepared clean and violating compilations.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>The empty-class compilation used to isolate startup cost.</summary>
    private AnalyzerBenchmarkScenario _startup;

    /// <summary>Gets or sets the number of application types in the clean and violating corpora.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the three corpora and checks compilation and diagnostic counts outside measurement.</summary>
    /// <returns>A task representing benchmark preparation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _state = Ses1515ContentSecurityPolicyBenchmarkCases.Create(Nodes);
        _startup = Ses1515ContentSecurityPolicyBenchmarkCases.CreateStartup();
        await Ses1515ContentSecurityPolicyBenchmarkCases.ValidateAsync(_state, _startup, Nodes).ConfigureAwait(false);
    }

    /// <summary>Measures analyzer initialization over exactly one empty class.</summary>
    /// <returns>The diagnostic count, which must be zero.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Startup() => AnalyzerBenchmarkRunner.GetDiagnosticCountAsync(_startup, _state.Analyzers);

    /// <summary>Measures ordinary text, safe policy sources and real string comparison operands.</summary>
    /// <returns>The diagnostic count, which must be zero.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Measures permissive headers alongside the clean workload.</summary>
    /// <returns>The diagnostic count checked against the generated corpus during setup.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);
}
