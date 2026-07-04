// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags capturing lambdas passed to methods whose own overload set has a state-taking twin
/// (PSH1011): the same member with one extra parameter typed <c>object</c> or a method-level
/// type parameter, whose callback delegate can receive the captured data instead —
/// <c>ContinueWith(static (t, state) => ..., state)</c>, <c>UnsafeRegister</c>,
/// <c>QueueUserWorkItem</c>, and scheduler-style APIs. A static lambda plus state allocates
/// neither closure nor per-call delegate. The capture analysis runs last, only after a
/// sibling overload is found, and only captures declared outside the lambda count.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1011UseStateOverloadAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.UseStateOverload);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeLambda,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression);
    }

    /// <summary>Reports PSH1011 for a capturing lambda whose consumer offers a state overload.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLambda(SyntaxNodeAnalysisContext context)
    {
        var lambda = (AnonymousFunctionExpressionSyntax)context.Node;
        if (lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
            || lambda.Parent is not ArgumentSyntax { NameColon: null, Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax outer } argumentList } argument)
        {
            return;
        }

        var model = context.SemanticModel;
        if (model.GetSymbolInfo(outer, context.CancellationToken).Symbol is not IMethodSymbol bound)
        {
            return;
        }

        var method = bound.ReducedFrom ?? bound;
        var index = argumentList.Arguments.IndexOf(argument) + (bound.ReducedFrom is null ? 0 : 1);
        if (index >= method.Parameters.Length
            || GetDelegateArity(method.Parameters[index].Type) is not { } arity
            || !HasStateOverload(method, arity))
        {
            return;
        }

        if (!CapturesOuterState(model, lambda))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.UseStateOverload,
            lambda.SyntaxTree,
            lambda.Span,
            method.Name));
    }

    /// <summary>Returns the parameter count of a delegate type's Invoke method.</summary>
    /// <param name="type">The candidate delegate type.</param>
    /// <returns>The arity, or <see langword="null"/> for non-delegates.</returns>
    private static int? GetDelegateArity(ITypeSymbol type)
        => type is INamedTypeSymbol { TypeKind: TypeKind.Delegate, DelegateInvokeMethod: { } invoke }
            ? invoke.Parameters.Length
            : null;

    /// <summary>Scans the method group for a same-name overload adding a state parameter.</summary>
    /// <param name="method">The bound method, unreduced.</param>
    /// <param name="callbackArity">The current callback delegate's arity.</param>
    /// <returns><see langword="true"/> when a state-taking twin exists.</returns>
    private static bool HasStateOverload(IMethodSymbol method, int callbackArity)
    {
        foreach (var member in method.ContainingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol sibling
                && !SymbolEqualityComparer.Default.Equals(sibling.OriginalDefinition, method.OriginalDefinition)
                && sibling.IsStatic == method.IsStatic
                && sibling.Parameters.Length == method.Parameters.Length + 1
                && HasStateShape(sibling, callbackArity))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an overload carries a state parameter and a callback that can receive it.</summary>
    /// <param name="sibling">The candidate overload.</param>
    /// <param name="callbackArity">The current callback delegate's arity.</param>
    /// <returns><see langword="true"/> when the overload has the callback-and-state shape.</returns>
    private static bool HasStateShape(IMethodSymbol sibling, int callbackArity)
    {
        var hasState = false;
        var hasCallback = false;
        foreach (var parameter in sibling.Parameters)
        {
            if (parameter.Type.SpecialType == SpecialType.System_Object
                || parameter.Type is ITypeParameterSymbol { DeclaringMethod: not null })
            {
                hasState = true;
            }
            else if (GetDelegateArity(parameter.Type) is { } arity && (arity == callbackArity || arity == callbackArity + 1))
            {
                hasCallback = true;
            }
        }

        return hasState && hasCallback;
    }

    /// <summary>Returns whether a lambda captures variables or <c>this</c> from the enclosing scope.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="lambda">The lambda to analyze.</param>
    /// <returns><see langword="true"/> when at least one outer symbol is captured.</returns>
    private static bool CapturesOuterState(SemanticModel model, AnonymousFunctionExpressionSyntax lambda)
    {
        var dataFlow = model.AnalyzeDataFlow(lambda);
        if (dataFlow?.Succeeded != true)
        {
            return false;
        }

        foreach (var symbol in dataFlow.Captured)
        {
            if (symbol.Locations.Length == 0 || !lambda.Span.Contains(symbol.Locations[0].SourceSpan))
            {
                return true;
            }
        }

        return false;
    }
}
