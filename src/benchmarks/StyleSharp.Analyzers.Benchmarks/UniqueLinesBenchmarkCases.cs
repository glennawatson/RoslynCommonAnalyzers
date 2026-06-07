// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for the unique-lines analyzer family.</summary>
internal static class UniqueLinesBenchmarkCases
{
    /// <summary>Creates prepared benchmark state for SST1151.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateMethodDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1151MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateMethodDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1154.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateInvocationArguments(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1154InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateInvocationArguments,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1155.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateObjectCreationArguments(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1155ObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateObjectCreationArguments,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1170.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateTypeArgumentLists(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1170TypeArgumentListMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateTypeArgumentLists,
            nodes);
}
