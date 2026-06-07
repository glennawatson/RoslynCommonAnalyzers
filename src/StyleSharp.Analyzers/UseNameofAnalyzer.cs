// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a string literal in an <c>Argument*Exception</c> constructor that names an in-scope
/// parameter, where <c>nameof</c> would track renames (SST1415). The scope is deliberately narrow
/// — only argument-exception constructions and only literals equal to an enclosing parameter name —
/// so unrelated strings are never flagged. Detection is purely syntactic: an object creation that
/// is not an argument exception is rejected by a cheap name check before anything else.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseNameofAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UseNameofForParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    }

    /// <summary>Counts the string-literal constructor arguments that should become <c>nameof</c>.</summary>
    /// <param name="creation">The candidate object creation.</param>
    /// <returns>The number of matching parameter-name literals.</returns>
    internal static int CountParameterNameLiteralMatches(ObjectCreationExpressionSyntax creation)
    {
        if (!IsArgumentExceptionType(creation.Type)
            || creation.ArgumentList is not { Arguments.Count: > 0 } arguments)
        {
            return 0;
        }

        var matches = 0;
        for (var i = 0; i < arguments.Arguments.Count; i++)
        {
            if (arguments.Arguments[i].Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)
                && IsVisibleParameterName(creation, literal.Token.ValueText))
            {
                matches++;
            }
        }

        return matches;
    }

    /// <summary>Reports SST1415 for parameter-naming string literals in an argument-exception constructor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (!IsArgumentExceptionType(creation.Type)
            || creation.ArgumentList is not { Arguments.Count: > 0 } arguments)
        {
            return;
        }

        for (var i = 0; i < arguments.Arguments.Count; i++)
        {
            if (arguments.Arguments[i].Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)
                && IsVisibleParameterName(creation, literal.Token.ValueText))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.UseNameofForParameter, literal.SyntaxTree, literal.Span, literal.Token.ValueText));
            }
        }
    }

    /// <summary>Returns whether a type is an <c>Argument…Exception</c>.</summary>
    /// <param name="type">The created type.</param>
    /// <returns><see langword="true"/> for a type named <c>Argument*Exception</c>.</returns>
    private static bool IsArgumentExceptionType(TypeSyntax type)
    {
        var name = type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null
        };

        return name is { } simpleName
            && simpleName.StartsWith("Argument", StringComparison.Ordinal)
            && simpleName.EndsWith("Exception", StringComparison.Ordinal);
    }

    /// <summary>Returns whether a parameter name is visible at a node, up to and including the enclosing method.</summary>
    /// <param name="start">The node to search outward from.</param>
    /// <param name="candidateName">The candidate parameter name.</param>
    /// <returns><see langword="true"/> when a visible parameter matches the candidate name.</returns>
    private static bool IsVisibleParameterName(SyntaxNode start, string candidateName)
    {
        for (var current = start.Parent; current is not null; current = current.Parent)
        {
            if (DeclaresParameterName(current, candidateName))
            {
                return true;
            }

            if (current is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                break;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node declares a parameter with the candidate name.</summary>
    /// <param name="node">The candidate parameter-owning node.</param>
    /// <param name="candidateName">The candidate parameter name.</param>
    /// <returns><see langword="true"/> when the node directly declares a matching parameter.</returns>
    private static bool DeclaresParameterName(SyntaxNode node, string candidateName) =>
        node switch
        {
            BaseMethodDeclarationSyntax method => ContainsParameterName(method.ParameterList.Parameters, candidateName),
            LocalFunctionStatementSyntax localFunction => ContainsParameterName(localFunction.ParameterList.Parameters, candidateName),
            ParenthesizedLambdaExpressionSyntax lambda => ContainsParameterName(lambda.ParameterList.Parameters, candidateName),
            IndexerDeclarationSyntax indexer => ContainsParameterName(indexer.ParameterList.Parameters, candidateName),
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.ValueText == candidateName,
            _ => false
        };

    /// <summary>Returns whether any parameter in the list matches the candidate name.</summary>
    /// <param name="parameters">The parameter list to scan.</param>
    /// <param name="candidateName">The candidate parameter name.</param>
    /// <returns><see langword="true"/> when a matching parameter is present.</returns>
    private static bool ContainsParameterName(SeparatedSyntaxList<ParameterSyntax> parameters, string candidateName)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Identifier.ValueText == candidateName)
            {
                return true;
            }
        }

        return false;
    }
}
