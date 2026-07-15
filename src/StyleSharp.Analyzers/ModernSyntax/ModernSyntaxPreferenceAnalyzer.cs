// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports narrow C# syntax preferences that keep local code compact (SST2218-SST2219).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModernSyntaxPreferenceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The number of same-name methods that requires overload-safe speculative binding.</summary>
    private const int OverloadRiskThreshold = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernSyntaxRules.UseImplicitLambdaParameterTypes,
        ModernSyntaxRules.SimplifyPropertyAccessor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.ParenthesizedLambdaExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeAccessor,
            SyntaxKind.GetAccessorDeclaration,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.InitAccessorDeclaration);
    }

    /// <summary>Returns whether the lambda can drop explicit parameter types.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <returns><see langword="true"/> when every parameter has a simple removable type.</returns>
    internal static bool CanUseImplicitParameterTypes(ParenthesizedLambdaExpressionSyntax lambda)
    {
        var parameters = lambda.ParameterList.Parameters;
        if (parameters.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (parameter.Type is null
                || parameter.Modifiers.Count != 0
                || parameter.AttributeLists.Count != 0
                || parameter.Default is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the expression represented by a simple accessor body.</summary>
    /// <param name="accessor">The accessor.</param>
    /// <param name="expression">The expression.</param>
    /// <returns><see langword="true"/> when the accessor has a single expression-shaped body.</returns>
    internal static bool TryGetAccessorExpression(AccessorDeclarationSyntax accessor, out ExpressionSyntax expression)
    {
        expression = null!;
        if (accessor.ExpressionBody is not null
            || accessor.Body is not { Statements.Count: 1 })
        {
            return false;
        }

        switch (accessor.Body.Statements[0])
        {
            case ReturnStatementSyntax { Expression: { } returned } when accessor.IsKind(SyntaxKind.GetAccessorDeclaration):
            {
                expression = returned;
                return true;
            }

            case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } when accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                || accessor.IsKind(SyntaxKind.InitAccessorDeclaration):
            {
                expression = assignment;
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Reports lambdas whose parameter types are already supplied by the target delegate.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeLambda(SyntaxNodeAnalysisContext context)
    {
        var lambda = (ParenthesizedLambdaExpressionSyntax)context.Node;
        if (!CanUseImplicitParameterTypes(lambda)
            || (!HasExplicitLambdaTarget(lambda)
                && context.SemanticModel.GetTypeInfo(lambda, context.CancellationToken).ConvertedType is null)
            || !OmittingParameterTypesPreservesCallBinding(lambda, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseImplicitLambdaParameterTypes, lambda.ParameterList.GetLocation()));
    }

    /// <summary>Reports single-statement accessors that can use an expression body.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeAccessor(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7 })
        {
            return;
        }

        var accessor = (AccessorDeclarationSyntax)context.Node;
        if (!TryGetAccessorExpression(accessor, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.SimplifyPropertyAccessor, accessor.Keyword.GetLocation()));
    }

    /// <summary>Returns whether syntax already supplies a direct explicit target for the lambda.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <returns><see langword="true"/> for explicit variable or field initializers.</returns>
    private static bool HasExplicitLambdaTarget(ParenthesizedLambdaExpressionSyntax lambda)
        => lambda.Parent is EqualsValueClauseSyntax
        {
            Parent: VariableDeclaratorSyntax
            {
                Parent: VariableDeclarationSyntax
                {
                    Type: not IdentifierNameSyntax { Identifier.Text: "var" }
                }
            }
        };

    /// <summary>Returns whether removing lambda parameter types keeps an overloadable call bound to the same symbol.</summary>
    /// <param name="lambda">The lambda to simplify.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when no overloadable call changes binding.</returns>
    private static bool OmittingParameterTypesPreservesCallBinding(
        ParenthesizedLambdaExpressionSyntax lambda,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (lambda.Parent is not ArgumentSyntax { Parent: BaseArgumentListSyntax { Parent: ExpressionSyntax call } }
            || call is not InvocationExpressionSyntax and not ObjectCreationExpressionSyntax)
        {
            return true;
        }

        var originalSymbol = SymbolResolution.GetSingleSymbol(model.GetSymbolInfo(call, cancellationToken));
        if (originalSymbol is not IMethodSymbol originalMethod)
        {
            return false;
        }

        if (!RequiresSpeculativeCallBinding(call, originalMethod))
        {
            return true;
        }

        var replacement = RemoveLambdaParameterTypes(lambda);
        var rewrittenCall = call.ReplaceNode(lambda, replacement);
        var rewrittenSymbol = SymbolResolution.GetSingleSymbol(model.GetSpeculativeSymbolInfo(
            call.SpanStart,
            rewrittenCall,
            SpeculativeBindingOption.BindAsExpression));

        return SymbolEqualityComparer.Default.Equals(originalSymbol, rewrittenSymbol);
    }

    /// <summary>Returns whether a lambda simplification needs a speculative call rebind.</summary>
    /// <param name="call">The call containing the lambda argument.</param>
    /// <param name="originalMethod">The method selected by the original call.</param>
    /// <returns><see langword="true"/> when overload resolution or type inference may change.</returns>
    private static bool RequiresSpeculativeCallBinding(
        ExpressionSyntax call,
        IMethodSymbol originalMethod)
    {
        if (originalMethod.IsGenericMethod && !HasExplicitTypeArguments(call))
        {
            return true;
        }

        return call is not InvocationExpressionSyntax
            || !HasNoContainingTypeOverloads(originalMethod);
    }

    /// <summary>Returns whether the call has explicit generic method type arguments.</summary>
    /// <param name="call">The call expression.</param>
    /// <returns><see langword="true"/> when the method type arguments are written in source.</returns>
    private static bool HasExplicitTypeArguments(ExpressionSyntax call)
        => call is InvocationExpressionSyntax
        {
            Expression: GenericNameSyntax or MemberAccessExpressionSyntax { Name: GenericNameSyntax }
        };

    /// <summary>Returns whether the selected method has no same-name overloads on its containing type hierarchy.</summary>
    /// <param name="originalMethod">The method selected by the original call.</param>
    /// <returns><see langword="true"/> when no overload can be selected by the lambda parameter types.</returns>
    private static bool HasNoContainingTypeOverloads(IMethodSymbol originalMethod)
    {
        if (originalMethod.IsExtensionMethod || originalMethod.ReducedFrom is not null)
        {
            return false;
        }

        var containingType = originalMethod.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var methodCount = 0;
        for (var current = containingType; current is not null; current = current.BaseType)
        {
            methodCount += CountMatchingMethods(current.GetMembers(originalMethod.Name), originalMethod, OverloadRiskThreshold - methodCount);
            if (methodCount > 1)
            {
                return false;
            }
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            methodCount += CountMatchingMethods(interfaces[i].GetMembers(originalMethod.Name), originalMethod, OverloadRiskThreshold - methodCount);
            if (methodCount > 1)
            {
                return false;
            }
        }

        return methodCount == 1;
    }

    /// <summary>Counts same-kind methods up to the requested maximum.</summary>
    /// <param name="members">The members to inspect.</param>
    /// <param name="originalMethod">The method selected by the original call.</param>
    /// <param name="maximum">The maximum useful count.</param>
    /// <returns>The number of matching methods found, capped at <paramref name="maximum"/>.</returns>
    private static int CountMatchingMethods(ImmutableArray<ISymbol> members, IMethodSymbol originalMethod, int maximum)
    {
        var count = 0;
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IMethodSymbol method
                || method.MethodKind != originalMethod.MethodKind)
            {
                continue;
            }

            count++;
            if (count >= maximum)
            {
                return count;
            }
        }

        return count;
    }

    /// <summary>Removes explicit parameter types from a lambda.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <returns>The updated lambda.</returns>
    private static ParenthesizedLambdaExpressionSyntax RemoveLambdaParameterTypes(ParenthesizedLambdaExpressionSyntax lambda)
    {
        var parameters = lambda.ParameterList.Parameters;
        var parametersWithSeparators = parameters.GetWithSeparators();
        var rewritten = new SyntaxNodeOrToken[parametersWithSeparators.Count];
        for (var i = 0; i < parametersWithSeparators.Count; i++)
        {
            rewritten[i] = parametersWithSeparators[i].AsNode() is ParameterSyntax parameter
                ? parameter.WithType(null)
                : parametersWithSeparators[i];
        }

        return lambda.WithParameterList(lambda.ParameterList.WithParameters(SyntaxFactory.SeparatedList<ParameterSyntax>(rewritten)));
    }
}
