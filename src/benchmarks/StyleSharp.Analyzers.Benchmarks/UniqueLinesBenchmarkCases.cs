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

    /// <summary>Creates prepared benchmark state for SST1150.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateConstructorDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1150ConstructorDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateConstructorDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1152.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateDelegateDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1152DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateDelegateDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1153.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateIndexerDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1153IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateIndexerDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1156.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateElementAccessArguments(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateElementAccessArguments,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1157.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateAttributeArguments(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1157AttributeArgumentMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateAttributeArguments,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1158.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateAnonymousMethodExpressionParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1158AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateAnonymousMethodExpressionParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1159.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateParenthesizedLambdaExpressionParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1159ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateParenthesizedLambdaExpressionParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1160.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateRecordDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1160RecordDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateRecordDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1161.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateClassDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1161ClassDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateClassDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1162.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateStructDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1162StructDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateStructDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1163.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateImplicitObjectCreationArguments(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateImplicitObjectCreationArguments,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1164.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateConstructorInitializerArguments(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1164ConstructorInitializerArgumentMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateConstructorInitializerArguments,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1165.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreatePrimaryConstructorBaseTypeArguments(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GeneratePrimaryConstructorBaseTypeArguments,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1166.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateLocalFunctionStatementParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1166LocalFunctionStatementParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateLocalFunctionStatementParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1167.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateOperatorDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1167OperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateOperatorDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1168.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateConversionOperatorDeclarationParameters(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateConversionOperatorDeclarationParameters,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1169.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateTypeParameterLists(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1169TypeParameterListMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateTypeParameterLists,
            nodes);

    /// <summary>Creates prepared benchmark state for SST1171.</summary>
    /// <param name="nodes">The synthetic node count.</param>
    /// <returns>The prepared benchmark state.</returns>
    public static SingleAnalyzerBenchmarkState CreateFunctionPointerParameterLists(int nodes)
        => SingleAnalyzerBenchmarkCases.Create(
            new Sst1171FunctionPointerParameterListMustBeOnUniqueLinesAnalyzer(),
            UniqueLinesBenchmarkSource.GenerateFunctionPointerParameterLists,
            nodes);
}
