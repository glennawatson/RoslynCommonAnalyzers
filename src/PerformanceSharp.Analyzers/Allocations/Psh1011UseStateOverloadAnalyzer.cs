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
/// The twin must <em>add</em> a state parameter: an <c>object</c> parameter the current
/// overload already declares is not caller state. Keyed dependency-injection registration
/// is the motivating counter-example — <c>AddKeyedSingleton(Type, object?, factory)</c>
/// widens <c>AddKeyedSingleton&lt;T&gt;(object?, factory)</c> with a service type, and the
/// <c>object?</c> both declare is the service key the factory receives, not state.
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
        if (!TryGetInvocationArgument(lambda, out var outer, out var argumentList, out var argument))
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
            || method.Parameters[index].Type is not { } callbackType
            || GetDelegateInvoke(callbackType) is not { } callbackInvoke
            || !HasStateOverload(method, callbackInvoke))
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

    /// <summary>Returns the invocation and argument carrying a non-static, positional anonymous function.</summary>
    /// <param name="lambda">The anonymous function being analyzed.</param>
    /// <param name="outer">The containing invocation.</param>
    /// <param name="argumentList">The containing argument list.</param>
    /// <param name="argument">The argument that contains the anonymous function.</param>
    /// <returns><see langword="true"/> when the lambda sits in a positional invocation argument.</returns>
    private static bool TryGetInvocationArgument(
        AnonymousFunctionExpressionSyntax lambda,
        [NotNullWhen(true)] out InvocationExpressionSyntax? outer,
        [NotNullWhen(true)] out ArgumentListSyntax? argumentList,
        [NotNullWhen(true)] out ArgumentSyntax? argument)
    {
        outer = null;
        argumentList = null;
        argument = null;
        if (lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
            || lambda.Parent is not ArgumentSyntax { NameColon: null, Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } arguments } lambdaArgument)
        {
            return false;
        }

        outer = invocation;
        argumentList = arguments;
        argument = lambdaArgument;
        return true;
    }

    /// <summary>Returns a delegate type's Invoke method.</summary>
    /// <param name="type">The candidate delegate type.</param>
    /// <returns>The Invoke method, or <see langword="null"/> for non-delegates.</returns>
    private static IMethodSymbol? GetDelegateInvoke(ITypeSymbol type)
        => type is INamedTypeSymbol { TypeKind: TypeKind.Delegate, DelegateInvokeMethod: { } invoke }
            ? invoke
            : null;

    /// <summary>Scans the method group for a same-name overload adding a state parameter.</summary>
    /// <param name="method">The bound method, unreduced.</param>
    /// <param name="callbackInvoke">The current callback delegate's Invoke method.</param>
    /// <returns><see langword="true"/> when a state-taking twin exists.</returns>
    private static bool HasStateOverload(IMethodSymbol method, IMethodSymbol callbackInvoke)
    {
        var methodStateCount = CountStateParameters(method.OriginalDefinition);
        foreach (var member in method.ContainingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol sibling
                && !SymbolEqualityComparer.Default.Equals(sibling.OriginalDefinition, method.OriginalDefinition)
                && sibling.IsStatic == method.IsStatic
                && sibling.Parameters.Length == method.Parameters.Length + 1
                && CountStateParameters(sibling.OriginalDefinition) > methodStateCount
                && HasStateCallback(sibling, callbackInvoke))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts the parameters an overload could pass to its callback as caller-supplied state.</summary>
    /// <param name="method">The method definition to scan.</param>
    /// <returns>The number of <c>object</c> or method-type-parameter parameters.</returns>
    /// <remarks>
    /// Counted on the original definition so an inferred type argument cannot turn a
    /// <c>TState</c> parameter into a concrete type and hide it.
    /// </remarks>
    private static int CountStateParameters(IMethodSymbol method)
    {
        var count = 0;
        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type.SpecialType == SpecialType.System_Object
                || parameter.Type is ITypeParameterSymbol { DeclaringMethod: not null })
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Returns whether an overload has a callback that can receive the state argument.</summary>
    /// <param name="sibling">The candidate overload.</param>
    /// <param name="callbackInvoke">The current callback delegate's Invoke method.</param>
    /// <returns><see langword="true"/> when one parameter is a state-carrying callback.</returns>
    private static bool HasStateCallback(IMethodSymbol sibling, IMethodSymbol callbackInvoke)
    {
        foreach (var parameter in sibling.Parameters)
        {
            if (GetDelegateInvoke(parameter.Type) is { } siblingInvoke && IsStateCallbackShape(siblingInvoke, callbackInvoke))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a candidate callback can carry state without dropping existing callback inputs.</summary>
    /// <param name="siblingInvoke">The candidate callback delegate's Invoke method.</param>
    /// <param name="callbackInvoke">The original callback delegate's Invoke method.</param>
    /// <returns><see langword="true"/> when the candidate preserves the original callback shape and can receive state.</returns>
    /// <remarks>
    /// The extra parameter has to be the state and nothing else. Matching on arity alone made any overload
    /// that happened to take one more argument look like a state overload: a recursive scheduling call whose
    /// callback receives the continuation to recurse through was answered with an overload whose callback
    /// receives a scheduler instead, so the suggestion silently dropped the one argument the call was about.
    /// The original callback's parameters must survive the move, either before the state slot or after it.
    /// </remarks>
    private static bool IsStateCallbackShape(IMethodSymbol siblingInvoke, IMethodSymbol callbackInvoke)
    {
        var siblingParameters = siblingInvoke.Parameters;
        var callbackParameters = callbackInvoke.Parameters;
        if (siblingParameters.Length == callbackParameters.Length + 1)
        {
            return PreservesCallbackParameters(siblingParameters, callbackParameters, stateOffset: 0)
                || PreservesCallbackParameters(siblingParameters, callbackParameters, stateOffset: 1);
        }

        return siblingParameters.Length == callbackParameters.Length
            && PreservesCallbackParameters(siblingParameters, callbackParameters, stateOffset: 0);
    }

    /// <summary>Returns whether the original callback's parameters survive alongside the state slot.</summary>
    /// <param name="siblingParameters">The candidate callback's parameters.</param>
    /// <param name="callbackParameters">The original callback's parameters.</param>
    /// <param name="stateOffset">1 when the state comes first, 0 when it comes last.</param>
    /// <returns><see langword="true"/> when every original parameter is still there, in order, by type.</returns>
    private static bool PreservesCallbackParameters(
        ImmutableArray<IParameterSymbol> siblingParameters,
        ImmutableArray<IParameterSymbol> callbackParameters,
        int stateOffset)
    {
        for (var i = 0; i < callbackParameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(siblingParameters[i + stateOffset].Type, callbackParameters[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a lambda captures variables or <c>this</c> from the enclosing scope.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="lambda">The lambda to analyze.</param>
    /// <returns><see langword="true"/> when at least one outer symbol is captured.</returns>
    /// <remarks>
    /// Only <see cref="DataFlowAnalysis.CapturedInside"/> answers the question. <c>Captured</c> is
    /// <c>CapturedInside</c> together with <c>CapturedOutside</c>, and <c>CapturedOutside</c> holds what the
    /// <em>other</em> lambdas in the enclosing method captured. Reading it charged this lambda with its
    /// neighbours' captures: a lambda that closed over nothing was told to move state it never had, purely
    /// because something else nearby closed over something.
    /// </remarks>
    private static bool CapturesOuterState(SemanticModel model, AnonymousFunctionExpressionSyntax lambda)
    {
        var dataFlow = model.AnalyzeDataFlow(lambda);
        if (dataFlow?.Succeeded != true)
        {
            return false;
        }

        foreach (var symbol in dataFlow.CapturedInside)
        {
            if (symbol.Locations.Length == 0 || !lambda.Span.Contains(symbol.Locations[0].SourceSpan))
            {
                return true;
            }
        }

        return false;
    }
}
