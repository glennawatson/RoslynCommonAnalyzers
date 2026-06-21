// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports C# 12 collection-expression opportunities beyond simple creation syntax (SST2102-SST2105).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionExpressionAdvancedAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        CollectionExpressionRules.UseCollectionExpressionForStackalloc,
        CollectionExpressionRules.UseCollectionExpressionForCreate,
        CollectionExpressionRules.UseCollectionExpressionForBuilder,
        CollectionExpressionRules.UseCollectionExpressionForFluent);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(start =>
        {
            var hasCollectionBuilderAttribute = CollectionExpressionAdvancedAnalysis.HasCollectionBuilderAttribute(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeStackalloc(nodeContext, hasCollectionBuilderAttribute),
                SyntaxKind.StackAllocArrayCreationExpression,
                SyntaxKind.ImplicitStackAllocArrayCreationExpression);
            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeBuilderLocal(nodeContext, hasCollectionBuilderAttribute),
                SyntaxKind.LocalDeclarationStatement);
        });
    }

    /// <summary>Reports stackalloc initializers that can be target-typed as collection expressions.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="hasCollectionBuilderAttribute">Whether the referenced framework supports C# 12 collection builders.</param>
    private static void AnalyzeStackalloc(SyntaxNodeAnalysisContext context, bool hasCollectionBuilderAttribute)
    {
        if (!hasCollectionBuilderAttribute
            || context.Node is not ExpressionSyntax expression
            || !CollectionExpressionHelper.IsLanguageSupported(expression)
            || !CollectionExpressionAdvancedAnalysis.TryGetStackallocInitializer(expression, out var initializer)
            || initializer.Expressions.Count == 0
            || !CollectionExpressionHelper.TryGetConvertedTypeWithExplicitTarget(context, expression, out var target)
            || !CollectionExpressionHelper.IsSpanTarget(target))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CollectionExpressionRules.UseCollectionExpressionForStackalloc, expression.GetLocation()));
    }

    /// <summary>Reports factory and fluent invocations that can be collection expressions.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!CollectionExpressionHelper.IsLanguageSupported(invocation)
            || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var methodName } access)
        {
            return;
        }

        if (methodName is "ToArray" or "ToList")
        {
            TryReportFluent(context, invocation, access);
            return;
        }

        TryReportCreate(context, invocation);
    }

    /// <summary>Reports short builder sequences that can be returned as collection expressions.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="hasCollectionBuilderAttribute">Whether the referenced framework supports C# 12 collection builders.</param>
    private static void AnalyzeBuilderLocal(SyntaxNodeAnalysisContext context, bool hasCollectionBuilderAttribute)
    {
        var local = (LocalDeclarationStatementSyntax)context.Node;
        if (!hasCollectionBuilderAttribute
            || !CollectionExpressionHelper.IsLanguageSupported(local)
            || !CollectionExpressionAdvancedAnalysis.TryGetBuilderSequence(local, out _, out var returnStatement)
            || local.Declaration.Variables[0].Initializer?.Value is not InvocationExpressionSyntax builderCreation
            || context.SemanticModel.GetSymbolInfo(builderCreation, context.CancellationToken).Symbol is not IMethodSymbol)
        {
            return;
        }

        var converted = returnStatement.Expression is null
            ? null
            : context.SemanticModel.GetTypeInfo(returnStatement.Expression, context.CancellationToken).ConvertedType;
        if (!CollectionExpressionAdvancedAnalysis.IsCollectionExpressionTarget(converted))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CollectionExpressionRules.UseCollectionExpressionForBuilder, local.GetLocation()));
    }

    /// <summary>Reports fluent materialization over an inline collection source.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="access">The member access expression.</param>
    private static void TryReportFluent(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access)
    {
        if (invocation.ArgumentList.Arguments.Count != 0
            || !CollectionExpressionAdvancedAnalysis.TryGetInlineInitializer(access.Expression, out var initializer)
            || initializer.Expressions.Count == 0
            || !CollectionExpressionHelper.TryGetConvertedTypeWithExplicitTarget(context, invocation, out _)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !CollectionExpressionAdvancedAnalysis.IsLinqMaterialization(method))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CollectionExpressionRules.UseCollectionExpressionForFluent, invocation.GetLocation()));
    }

    /// <summary>Reports collection-builder factory calls that can be expressed inline.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="invocation">The invocation.</param>
    private static void TryReportCreate(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (!CollectionExpressionHelper.TryGetConvertedTypeWithExplicitTarget(context, invocation, out var target)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !CollectionExpressionAdvancedAnalysis.TargetUsesBuilderMethod(target, method)
            || !CollectionExpressionAdvancedAnalysis.TryBuildInvocationCollectionExpression(invocation, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CollectionExpressionRules.UseCollectionExpressionForCreate, invocation.GetLocation()));
    }
}
