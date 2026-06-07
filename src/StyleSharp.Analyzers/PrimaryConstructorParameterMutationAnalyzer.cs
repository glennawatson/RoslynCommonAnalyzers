// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports assignments, increment/decrement operations, and <c>ref</c>/<c>out</c> argument passing
/// that target a class or struct primary-constructor parameter (SST1425). Those parameters are
/// captured into hidden mutable state, so mutating them later is usually surprising.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrimaryConstructorParameterMutationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(MaintainabilityRules.NoReassignedPrimaryConstructorParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeAssignment,
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.CoalesceAssignmentExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeIncrementOrDecrement,
            SyntaxKind.PreIncrementExpression,
            SyntaxKind.PreDecrementExpression,
            SyntaxKind.PostIncrementExpression,
            SyntaxKind.PostDecrementExpression);
        context.RegisterSyntaxNodeAction(AnalyzeArgument, SyntaxKind.Argument);
    }

    /// <summary>Returns whether the expression could syntactically name a class or struct primary-constructor parameter.</summary>
    /// <param name="expression">The potentially mutated expression.</param>
    /// <returns><see langword="true"/> when semantic binding is still required.</returns>
    internal static bool CouldReferencePrimaryConstructorParameter(ExpressionSyntax expression)
    {
        if (expression is not IdentifierNameSyntax identifier
            || !TryGetPrimaryConstructorParameters(expression, out var parameters))
        {
            return false;
        }

        var identifierText = identifier.Identifier.ValueText;
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Identifier.ValueText == identifierText)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports SST1425 for assignments whose target is a primary-constructor parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        ReportIfPrimaryConstructorParameter(context, assignment.Left);
    }

    /// <summary>Reports SST1425 for ++/-- operations whose operand is a primary-constructor parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeIncrementOrDecrement(SyntaxNodeAnalysisContext context)
    {
        var expression = (ExpressionSyntax)context.Node;
        if (expression is PrefixUnaryExpressionSyntax prefix)
        {
            ReportIfPrimaryConstructorParameter(context, prefix.Operand);
            return;
        }

        ReportIfPrimaryConstructorParameter(context, ((PostfixUnaryExpressionSyntax)expression).Operand);
    }

    /// <summary>Reports SST1425 for <c>ref</c>/<c>out</c> arguments that target a primary-constructor parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeArgument(SyntaxNodeAnalysisContext context)
    {
        var argument = (ArgumentSyntax)context.Node;
        if (argument.RefKindKeyword.Kind() is not (SyntaxKind.RefKeyword or SyntaxKind.OutKeyword))
        {
            return;
        }

        ReportIfPrimaryConstructorParameter(context, argument.Expression);
    }

    /// <summary>Reports SST1425 when an expression binds to a class/struct primary-constructor parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The mutated expression.</param>
    private static void ReportIfPrimaryConstructorParameter(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        if (!CouldReferencePrimaryConstructorParameter(expression)
            || context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol is not IParameterSymbol parameter
            || !IsPrimaryConstructorParameter(parameter))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                MaintainabilityRules.NoReassignedPrimaryConstructorParameter,
                expression.GetLocation(),
                parameter.Name));
    }

    /// <summary>Returns whether a parameter is declared by a class or struct primary constructor.</summary>
    /// <param name="parameter">The parameter symbol.</param>
    /// <returns><see langword="true"/> when the parameter belongs to a class/struct primary constructor.</returns>
    private static bool IsPrimaryConstructorParameter(IParameterSymbol parameter)
    {
        for (var i = 0; i < parameter.DeclaringSyntaxReferences.Length; i++)
        {
            if (parameter.DeclaringSyntaxReferences[i].GetSyntax() is not ParameterSyntax { Parent: ParameterListSyntax { Parent: TypeDeclarationSyntax type } })
            {
                continue;
            }

            if (type is RecordDeclarationSyntax)
            {
                return false;
            }

            if (type is ClassDeclarationSyntax or StructDeclarationSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the containing class or struct primary-constructor parameters, when present.</summary>
    /// <param name="expression">The expression being checked.</param>
    /// <param name="parameters">The containing primary-constructor parameters.</param>
    /// <returns><see langword="true"/> when the expression is inside a class or struct primary constructor.</returns>
    private static bool TryGetPrimaryConstructorParameters(ExpressionSyntax expression, out SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        for (var current = expression.Parent; current is not null; current = current.Parent)
        {
            if (current is not TypeDeclarationSyntax type)
            {
                continue;
            }

            if (type is RecordDeclarationSyntax
                || type is not ClassDeclarationSyntax and not StructDeclarationSyntax
                || type.ParameterList is not { Parameters.Count: > 0 } parameterList)
            {
                parameters = default;
                return false;
            }

            parameters = parameterList.Parameters;
            return true;
        }

        parameters = default;
        return false;
    }
}
