// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports lambdas that only forward their parameters to one method call (SST2239).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2239MethodGroupAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseMethodGroup);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeSimpleLambda, SyntaxKind.SimpleLambdaExpression);
        context.RegisterSyntaxNodeAction(AnalyzeParenthesizedLambda, SyntaxKind.ParenthesizedLambdaExpression);
    }

    /// <summary>Reports simple lambdas that forward their single parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSimpleLambda(SyntaxNodeAnalysisContext context)
    {
        var lambda = (SimpleLambdaExpressionSyntax)context.Node;
        if (!IsForwardingLambda(lambda.Body, lambda.Parameter, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseMethodGroup, lambda.GetLocation()));
    }

    /// <summary>Reports parenthesized lambdas that forward every parameter in order.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeParenthesizedLambda(SyntaxNodeAnalysisContext context)
    {
        var lambda = (ParenthesizedLambdaExpressionSyntax)context.Node;
        var parameters = lambda.ParameterList.Parameters;
        if (parameters.Count == 0
            || lambda.Body is not InvocationExpressionSyntax invocation
            || !IsResolvedMethodCall(invocation, context.SemanticModel, context.CancellationToken)
            || !ArgumentsMatchParameters(invocation.ArgumentList.Arguments, parameters))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseMethodGroup, lambda.GetLocation()));
    }

    /// <summary>Returns whether a simple lambda forwards its parameter to one method call.</summary>
    /// <param name="body">The lambda body.</param>
    /// <param name="parameter">The lambda parameter.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the lambda can be represented by the invoked method group.</returns>
    private static bool IsForwardingLambda(
        CSharpSyntaxNode body,
        ParameterSyntax parameter,
        SemanticModel model,
        CancellationToken cancellationToken)
        => body is InvocationExpressionSyntax invocation
            && IsResolvedMethodCall(invocation, model, cancellationToken)
            && invocation.ArgumentList.Arguments.Count == 1
            && IsPlainIdentifierArgument(invocation.ArgumentList.Arguments[0], parameter.Identifier.ValueText);

    /// <summary>Returns whether an invocation resolves to a normal method call.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the call target is known.</returns>
    private static bool IsResolvedMethodCall(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary };

    /// <summary>Returns whether invocation arguments are the lambda parameters in declaration order.</summary>
    /// <param name="arguments">The invocation arguments.</param>
    /// <param name="parameters">The lambda parameters.</param>
    /// <returns><see langword="true"/> when each argument forwards the matching parameter.</returns>
    private static bool ArgumentsMatchParameters(
        in SeparatedSyntaxList<ArgumentSyntax> arguments,
        in SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        if (arguments.Count != parameters.Count)
        {
            return false;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            if (!IsPlainIdentifierArgument(arguments[i], parameters[i].Identifier.ValueText))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an argument is a positional value argument for the named parameter.</summary>
    /// <param name="argument">The argument.</param>
    /// <param name="parameterName">The expected parameter name.</param>
    /// <returns><see langword="true"/> when the argument forwards the parameter unchanged.</returns>
    private static bool IsPlainIdentifierArgument(ArgumentSyntax argument, string parameterName)
        => argument.NameColon is null
            && argument.RefKindKeyword.RawKind == 0
            && argument.Expression is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == parameterName;
}
