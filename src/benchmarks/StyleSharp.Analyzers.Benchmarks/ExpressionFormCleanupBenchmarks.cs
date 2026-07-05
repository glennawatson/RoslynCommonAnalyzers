// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for expression-form cleanup analyzers.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ExpressionFormCleanupBenchmarks
{
    /// <summary>The prepared benchmark state.</summary>
    private SingleAnalyzerBenchmarkState _state = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Members { get; set; }

    /// <summary>Builds the benchmark state.</summary>
    [GlobalSetup]
    public void Setup() => _state = Cases.Create(Members);

    /// <summary>Benchmarks already-clean expression forms.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> ExpressionFormCleanup_Clean() => SingleAnalyzerBenchmarkHelper.RunCleanAsync(_state);

    /// <summary>Benchmarks reportable expression forms.</summary>
    /// <returns>The diagnostic count.</returns>
    [Benchmark]
    public Task<int> ExpressionFormCleanup_Violating() => SingleAnalyzerBenchmarkHelper.RunViolatingAsync(_state);

    /// <summary>Builds benchmark source for expression-form cleanup analyzers.</summary>
    private static class Source
    {
        /// <summary>The number of expression shapes cycled through the benchmark source.</summary>
        private const int ShapeCount = 3;

        /// <summary>Builds clean or reportable expression-form source.</summary>
        /// <param name="members">The number of synthetic members to emit.</param>
        /// <param name="violating">Whether to emit reportable expression forms.</param>
        /// <returns>The generated source text.</returns>
        public static string Generate(int members, bool violating)
            => $$"""
               namespace Bench;

               internal sealed class C
               {
                   private static void Notify(string propertyName) { }

               {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
               }
               """;

        /// <summary>Builds one expression-form member.</summary>
        /// <param name="index">The synthetic member index.</param>
        /// <param name="violating">Whether to emit the reportable form.</param>
        /// <returns>The generated member block.</returns>
        private static string GenerateMember(int index, bool violating)
            => (index % ShapeCount) switch
            {
                0 => violating
                    ? $"    public int Parentheses{index}(int value) => (value);"
                    : $"    public int Parentheses{index}(int value) => value;",
                1 => GenerateNameofMember(index, violating),
                _ => violating
                    ? $"    public bool Pattern{index}(object value) => !(value is null);"
                    : $"    public bool Pattern{index}(object value) => value is not null;"
            };

        /// <summary>Builds one symbol-name argument member.</summary>
        /// <param name="index">The synthetic member index.</param>
        /// <param name="violating">Whether to emit the string literal form.</param>
        /// <returns>The generated member block.</returns>
        private static string GenerateNameofMember(int index, bool violating)
        {
            var memberName = "Value" + index;
            return violating
                ? $$"""
                       private int {{memberName}} => {{index}};
                       public void Name{{index}}() => Notify("{{memberName}}");
                   """
                : $$"""
                       private int {{memberName}} => {{index}};
                       public void Name{{index}}() => Notify(nameof({{memberName}}));
                   """;
        }
    }

    /// <summary>Builds benchmark state for expression-form cleanup analyzers.</summary>
    private static class Cases
    {
        /// <summary>The analyzer set used by expression-form cleanup benchmarks.</summary>
        private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        [
            new Sst1459UnnecessaryParenthesesAnalyzer(),
            new Sst1463NameofLiteralAnalyzer(),
            new Sst2008IsNotPatternAnalyzer()
        ];

        /// <summary>Creates the prepared benchmark state for the requested member count.</summary>
        /// <param name="members">The synthetic member count.</param>
        /// <returns>The prepared benchmark state.</returns>
        public static SingleAnalyzerBenchmarkState Create(int members)
            => new(
                Analyzers,
                CreateScenario(members, violating: false),
                CreateScenario(members, violating: true));

        /// <summary>Builds one benchmark scenario.</summary>
        /// <param name="members">The synthetic member count.</param>
        /// <param name="violating">Whether to build the reportable scenario.</param>
        /// <returns>The prepared benchmark scenario.</returns>
        private static AnalyzerBenchmarkScenario CreateScenario(int members, bool violating)
            => new(BenchmarkCompilationFactory.CreateCompilation(Source.Generate(members, violating)).Compilation);
    }
}
