// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for suppressions whose diagnostic is disabled by configuration.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DisabledDiagnosticSuppressionBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Members { get; set; }

    /// <summary>Builds the benchmark state.</summary>
    [GlobalSetup]
    public void Setup() => _state = Cases.Create(Members);

    /// <summary>Benchmarks suppressions for diagnostics that remain enabled.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> DisabledDiagnosticSuppression_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks suppressions for diagnostics disabled by compilation options.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> DisabledDiagnosticSuppression_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Builds benchmark source for suppressions whose diagnostic is disabled by configuration.</summary>
    private static class Source
    {
        /// <summary>Builds source containing scoped suppressions for a synthetic diagnostic id.</summary>
        /// <param name="members">The number of synthetic members and suppressions to emit.</param>
        /// <returns>The generated source text.</returns>
        public static string Generate(int members)
            => $$"""
               using System.Diagnostics.CodeAnalysis;

               {{BenchmarkSourceText.JoinLines(members, GenerateSuppression)}}

               namespace Bench;

               internal sealed class C
               {
               {{BenchmarkSourceText.JoinBlocks(members, GenerateMember)}}
               }
               """;

        /// <summary>Builds one assembly suppression.</summary>
        /// <param name="index">The synthetic member index.</param>
        /// <returns>The generated suppression attribute.</returns>
        private static string GenerateSuppression(int index)
            => $"""[assembly: SuppressMessage("Style", "SST9999:Disabled rule", Justification = "Benchmark.", Scope = "member", Target = "M:Bench.C.M{index}")]""";

        /// <summary>Builds one target member.</summary>
        /// <param name="index">The synthetic member index.</param>
        /// <returns>The generated member declaration.</returns>
        private static string GenerateMember(int index) => $"    public void M{index}() {{ }}";
    }

    /// <summary>Builds benchmark state for suppressions whose diagnostic is disabled by configuration.</summary>
    private static class Cases
    {
        /// <summary>The diagnostic id used by the synthetic suppressions.</summary>
        private const string SuppressedRuleId = "SST9999";

        /// <summary>Creates the prepared benchmark state for the requested member count.</summary>
        /// <param name="members">The synthetic member count.</param>
        /// <returns>The prepared benchmark state.</returns>
        public static SingleAnalyzerBenchmarkState Create(int members)
            => SingleAnalyzerBenchmarkHelper.Create(
                new Sst1462DisabledDiagnosticSuppressionAnalyzer(),
                CreateScenario(members, ReportDiagnostic.Warn),
                CreateScenario(members, ReportDiagnostic.Suppress));

        /// <summary>Builds one benchmark scenario with the requested diagnostic configuration.</summary>
        /// <param name="members">The synthetic member count.</param>
        /// <param name="state">The diagnostic state to apply for the suppressed rule.</param>
        /// <returns>The prepared benchmark scenario.</returns>
        private static AnalyzerBenchmarkScenario CreateScenario(int members, ReportDiagnostic state)
        {
            var compilation = BenchmarkCompilationFactory.CreateCompilation(Source.Generate(members)).Compilation;
            var options = compilation.Options.WithSpecificDiagnosticOptions(ImmutableDictionary.Create<string, ReportDiagnostic>().Add(SuppressedRuleId, state));
            return new(compilation.WithOptions(options));
        }
    }
}
