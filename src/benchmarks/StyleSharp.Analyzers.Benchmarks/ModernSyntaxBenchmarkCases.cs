// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds shared benchmark state for the modern-C# analyzer suites.</summary>
internal static class ModernSyntaxBenchmarkCases
{
    /// <summary>Creates the prepared benchmark state for empty-collection-expression analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateEmptyCollectionExpression(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new EmptyCollectionExpressionAnalyzer(), ModernSyntaxBenchmarkSource.GenerateEmptyCollectionExpression, nodes);

    /// <summary>Creates the prepared benchmark state for explicit-collection-expression analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateExplicitCollectionExpression(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new ExplicitCollectionExpressionAnalyzer(), ModernSyntaxBenchmarkSource.GenerateExplicitCollectionExpression, nodes, ["SST2101"]);

    /// <summary>Creates the prepared benchmark state for nested-ternary analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateNestedTernary(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new NestedTernaryAnalyzer(), ModernSyntaxBenchmarkSource.GenerateNestedTernary, nodes, ["SST1147"]);

    /// <summary>Creates the prepared benchmark state for null-coalescing-precedence analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateNullCoalescingPrecedence(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new NullCoalescingPrecedenceAnalyzer(), ModernSyntaxBenchmarkSource.GenerateNullCoalescingPrecedence, nodes);

    /// <summary>Creates the prepared benchmark state for partial-element-access analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreatePartialElementAccess(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new PartialElementAccessAnalyzer(), ModernSyntaxBenchmarkSource.GeneratePartialElementAccess, nodes);

    /// <summary>Creates the prepared benchmark state for prefer-extension-block analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreatePreferExtensionBlock(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new PreferExtensionBlockAnalyzer(), ModernSyntaxBenchmarkSource.GeneratePreferExtensionBlock, nodes, ["SST1703"]);

    /// <summary>Creates the prepared benchmark state for prefer-field-keyword analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreatePreferFieldKeyword(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new PreferFieldKeywordAnalyzer(), ModernSyntaxBenchmarkSource.GeneratePreferFieldKeyword, nodes, ["SST2200"]);

    /// <summary>Creates the prepared benchmark state for prefer-or-pattern analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreatePreferOrPattern(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new PreferOrPatternAnalyzer(), ModernSyntaxBenchmarkSource.GeneratePreferOrPattern, nodes, ["SST1144"]);

    /// <summary>Creates the prepared benchmark state for query-clause layout analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateQueryClause(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new QueryClauseAnalyzer(), ModernSyntaxBenchmarkSource.GenerateQueryClause, nodes);

    /// <summary>Creates the prepared benchmark state for redundant-parentheses analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateRedundantParentheses(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new RedundantParenthesesAnalyzer(), ModernSyntaxBenchmarkSource.GenerateRedundantParentheses, nodes);

    /// <summary>Creates the prepared benchmark state for use-lambda-syntax analysis.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateUseLambdaSyntax(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(new UseLambdaSyntaxAnalyzer(), ModernSyntaxBenchmarkSource.GenerateUseLambdaSyntax, nodes);
}
