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

    /// <summary>Reports SST1415 for parameter-naming string literals in an argument-exception constructor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (!IsArgumentExceptionType(creation.Type)
            || creation.ArgumentList is not { Arguments.Count: > 0 } arguments
            || CollectParameterNames(creation) is not { Count: > 0 } parameterNames)
        {
            return;
        }

        for (var i = 0; i < arguments.Arguments.Count; i++)
        {
            if (arguments.Arguments[i].Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)
                && parameterNames.Contains(literal.Token.ValueText))
            {
                context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.UseNameofForParameter, literal.GetLocation(), literal.Token.ValueText));
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
            _ => null,
        };

        return name is { } simpleName
            && simpleName.StartsWith("Argument", StringComparison.Ordinal)
            && simpleName.EndsWith("Exception", StringComparison.Ordinal);
    }

    /// <summary>Collects the parameter names visible at a node, up to and including the enclosing method.</summary>
    /// <param name="start">The node to search outward from.</param>
    /// <returns>The set of parameter names, or <see langword="null"/> when there are none.</returns>
    private static HashSet<string>? CollectParameterNames(SyntaxNode start)
    {
        HashSet<string>? names = null;
        for (SyntaxNode? current = start.Parent; current is not null; current = current.Parent)
        {
            AddParameters(current, ref names);
            if (current is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                break;
            }
        }

        return names;
    }

    /// <summary>Adds the parameter names declared directly by a node to the set.</summary>
    /// <param name="node">The candidate parameter-owning node.</param>
    /// <param name="names">The accumulating set (created on first use).</param>
    private static void AddParameters(SyntaxNode node, ref HashSet<string>? names)
    {
        var parameters = node switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
            LocalFunctionStatementSyntax localFunction => localFunction.ParameterList.Parameters,
            ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList.Parameters,
            IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
            SimpleLambdaExpressionSyntax simpleLambda => SyntaxFactory.SingletonSeparatedList(simpleLambda.Parameter),
            _ => default,
        };

        for (var i = 0; i < parameters.Count; i++)
        {
            var name = parameters[i].Identifier.ValueText;
            if (name.Length > 0)
            {
                (names ??= []).Add(name);
            }
        }
    }
}
