// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for modern-syntax value analysis.</summary>
internal static class ModernSyntaxValueBenchmarkCases
{
    /// <summary>Analyzer-config options that turn on the hot-path LINQ rule for this batch.</summary>
    private static readonly BenchmarkAnalyzerConfigOptionsProvider OptionsProvider = new(
        new Dictionary<string, string>(),
        new Dictionary<string, string>
        {
            ["stylesharp.avoid_linq_on_hot_path"] = "true"
        });

    /// <summary>The opt-in diagnostics measured by this analyzer batch.</summary>
    private static readonly string[] EnabledRuleIds =
    [
        ModernSyntaxRules.MakeIgnoredExpressionValueExplicit.Id,
        ModernSyntaxRules.ConvertAnonymousObjectToTuple.Id,
        ModernSyntaxRules.AvoidLinqOnHotPath.Id
    ];

    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new ModernSyntaxValueAnalyzer(),
            CreateScenario(nodes, violating: false),
            CreateScenario(nodes, violating: true));

    /// <summary>Builds one benchmark scenario.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <param name="violating">Whether to build the violating source.</param>
    /// <returns>The benchmark scenario.</returns>
    private static AnalyzerBenchmarkScenario CreateScenario(int nodes, bool violating)
        => new(
            BenchmarkCompilationFactory.CreateCompilation(
                ModernSyntaxValueBenchmarkSource.Generate(nodes, violating),
                EnabledRuleIds).Compilation,
            OptionsProvider);
}
