// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for single-use-local inlining analysis.</summary>
internal static class InlineSingleUseLocalBenchmarkCases
{
    /// <summary>Forces the rule on, because it ships disabled and would otherwise measure a driver that skips it.</summary>
    private static readonly string[] EnabledRuleIds = [ModernSyntaxRules.InlineSingleUseLocal.Id];

    /// <summary>Creates the prepared benchmark state for the requested node count.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState Create(int nodes)
        => SingleAnalyzerBenchmarkHelper.Create(
            new Sst2266InlineSingleUseLocalAnalyzer(),
            new(BenchmarkCompilationFactory.CreateCompilation(InlineSingleUseLocalBenchmarkSource.Generate(nodes, violating: false), EnabledRuleIds).Compilation),
            new(BenchmarkCompilationFactory.CreateCompilation(InlineSingleUseLocalBenchmarkSource.Generate(nodes, violating: true), EnabledRuleIds).Compilation));
}
