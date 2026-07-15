// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an override that forwards to the method it overrides but drops one of its own optional
/// parameters from the <c>base</c> call (SST2425). The base substitutes its own default for the missing
/// argument, so whatever value the caller supplied is discarded before the base ever sees it.
/// </summary>
/// <remarks>
/// The clean path is a syntactic prepass: only a <c>base.Name(...)</c> invocation inside a method reaches
/// the semantic model. The invocation is then confirmed to target the enclosing method's overridden method,
/// and its argument-to-parameter mapping is read from the operation — a compiler-supplied default argument
/// whose parameter matches an optional parameter of the override is the dropped forward.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2425BaseCallDropsOptionalArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.OverrideDropsOptionalArgument);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one base call that drops an optional argument the override received.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax })
        {
            return;
        }

        if (invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } methodDeclaration)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not { OverriddenMethod: { } overridden } method)
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation
            || !SymbolEqualityComparer.Default.Equals(operation.TargetMethod, overridden))
        {
            return;
        }

        var arguments = operation.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.ArgumentKind == ArgumentKind.DefaultValue
                && argument.Parameter is { } baseParameter
                && HasOptionalParameterNamed(method, baseParameter.Name))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    CorrectnessRules.OverrideDropsOptionalArgument,
                    invocation.GetLocation(),
                    baseParameter.Name));
                return;
            }
        }
    }

    /// <summary>Returns whether the override declares an optional parameter of the given name.</summary>
    /// <param name="method">The override method symbol.</param>
    /// <param name="name">The parameter name to look for.</param>
    /// <returns><see langword="true"/> when a matching optional parameter exists.</returns>
    private static bool HasOptionalParameterNamed(IMethodSymbol method, string name)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].HasExplicitDefaultValue && string.Equals(parameters[i].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
