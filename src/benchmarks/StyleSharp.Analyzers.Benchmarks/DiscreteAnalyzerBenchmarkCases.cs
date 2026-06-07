// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for the discrete per-analyzer benchmark family.</summary>
internal static class DiscreteAnalyzerBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for multiple-statements-on-line analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateMultipleStatementsOnLine(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new MultipleStatementsOnLineAnalyzer(), DiscreteAnalyzerBenchmarkSource.GenerateMultipleStatementsOnLine, nodes);

    /// <summary>Creates the prepared benchmark state for conditional-operator-placement analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateConditionalOperatorPlacement(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new ConditionalOperatorPlacementAnalyzer(), DiscreteAnalyzerBenchmarkSource.GenerateConditionalOperatorPlacement, nodes);

    /// <summary>Creates the prepared benchmark state for trailing-comma analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateTrailingComma(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new TrailingCommaAnalyzer(), DiscreteAnalyzerBenchmarkSource.GenerateTrailingComma, nodes);

    /// <summary>Creates the prepared benchmark state for single-line-element analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateSingleLineElement(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new SingleLineElementAnalyzer(), DiscreteAnalyzerBenchmarkSource.GenerateSingleLineElement, nodes);

    /// <summary>Creates the prepared benchmark state for readable-conditions analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateUseReadableConditions(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new UseReadableConditionsAnalyzer(), DiscreteAnalyzerBenchmarkSource.GenerateUseReadableConditions, nodes);
}
