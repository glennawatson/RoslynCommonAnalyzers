// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports narrow C# syntax preferences that keep local code compact (SST2218-SST2219).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModernSyntaxPreferenceAnalyzer : DiagnosticAnalyzer
{
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
                && context.SemanticModel.GetTypeInfo(lambda, context.CancellationToken).ConvertedType is null))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseImplicitLambdaParameterTypes, lambda.ParameterList.GetLocation()));
    }

    /// <summary>Reports single-statement accessors that can use an expression body.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeAccessor(SyntaxNodeAnalysisContext context)
    {
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
}
