// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for semantic and type-oriented analyzer suites.</summary>
internal static class SemanticTypeBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for trivial-auto-property analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateTrivialAutoProperty(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new TrivialAutoPropertyAnalyzer(), SemanticTypeBenchmarkSource.GenerateTrivialAutoProperty, nodes);

    /// <summary>Creates the prepared benchmark state for redundant-modifier analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateRedundantModifier(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new RedundantModifierAnalyzer(), SemanticTypeBenchmarkSource.GenerateRedundantModifier, nodes);

    /// <summary>Creates the prepared benchmark state for default-value-type-constructor analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateDefaultValueTypeConstructor(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new DefaultValueTypeConstructorAnalyzer(), SemanticTypeBenchmarkSource.GenerateDefaultValueTypeConstructor, nodes);

    /// <summary>Creates the prepared benchmark state for use-string-empty analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateUseStringEmpty(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new UseStringEmptyAnalyzer(), SemanticTypeBenchmarkSource.GenerateUseStringEmpty, nodes);

    /// <summary>Creates the prepared benchmark state for use-nullable-shorthand analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateUseNullableShorthand(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new UseNullableShorthandAnalyzer(), SemanticTypeBenchmarkSource.GenerateUseNullableShorthand, nodes);

    /// <summary>Creates the prepared benchmark state for use-tuple-syntax analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateUseTupleSyntax(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new UseTupleSyntaxAnalyzer(), SemanticTypeBenchmarkSource.GenerateUseTupleSyntax, nodes);

    /// <summary>Creates the prepared benchmark state for do-not-prefix-with-base analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateDoNotPrefixWithBase(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new DoNotPrefixWithBaseAnalyzer(), SemanticTypeBenchmarkSource.GenerateDoNotPrefixWithBase, nodes);
}
