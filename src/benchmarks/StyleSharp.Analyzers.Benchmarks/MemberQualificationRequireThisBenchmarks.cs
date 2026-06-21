// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for require-this member-qualification analysis.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MemberQualificationRequireThisBenchmarks
{
    /// <summary>The editorconfig key for instance-member qualification.</summary>
    private const string InstanceMemberQualificationKey = "stylesharp.instance_member_qualification";

    /// <summary>The benchmark options that require <c>this.</c> on instance members.</summary>
    private static readonly BenchmarkAnalyzerConfigOptionsProvider RequireThisOptions = new(
        new Dictionary<string, string>(),
        new Dictionary<string, string>
        {
            [InstanceMemberQualificationKey] = "require_this"
        });

    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the clean and violating scenarios once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
        => _state = SingleAnalyzerBenchmarkHelper.Create(
            new NameSimplificationAnalyzer(),
            CreateScenario(violating: false),
            CreateScenario(violating: true));

    /// <summary>Benchmarks the clean require-this member-qualification path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> MemberQualificationRequireThis_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks the violating require-this member-qualification path.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    [Benchmark]
    public Task<int> MemberQualificationRequireThis_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Creates one configured benchmark scenario.</summary>
    /// <param name="violating">Whether the source should contain unqualified instance members.</param>
    /// <returns>The prepared benchmark scenario.</returns>
    private AnalyzerBenchmarkScenario CreateScenario(bool violating)
        => new(
            BenchmarkCompilationFactory.CreateCompilation(ExpressionHotspotBenchmarkSource.Generate(Nodes, violating)).Compilation,
            RequireThisOptions);
}
