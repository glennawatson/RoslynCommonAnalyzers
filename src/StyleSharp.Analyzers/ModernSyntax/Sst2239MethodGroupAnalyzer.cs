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
        if (lambda.Body is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != 1
            || !IsPlainIdentifierArgument(arguments[0], lambda.Parameter.Identifier.ValueText)
            || !IsMethodGroupConvertible(invocation, lambda, arguments, context.SemanticModel, context.CancellationToken))
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
        if (parameters.Count == 0 || lambda.Body is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (!ArgumentsMatchParameters(arguments, parameters)
            || !IsMethodGroupConvertible(invocation, lambda, arguments, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseMethodGroup, lambda.GetLocation()));
    }

    /// <summary>Returns whether the lambda can be replaced by the invoked method group.</summary>
    /// <param name="invocation">The forwarding invocation in the lambda body.</param>
    /// <param name="lambda">The lambda being replaced.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the method group converts to the lambda's target type.</returns>
    /// <remarks>
    /// A method group conversion only ever uses a candidate's normal form and never applies
    /// optional-parameter defaults, so a call that omits optional parameters or expands a
    /// <see langword="params"/> parameter has no method-group equivalent. Expression trees
    /// cannot hold a method group at all.
    /// </remarks>
    private static bool IsMethodGroupConvertible(
        InvocationExpressionSyntax invocation,
        ExpressionSyntax lambda,
        in SeparatedSyntaxList<ArgumentSyntax> arguments,
        SemanticModel model,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary } method
            && method.Parameters.Length == arguments.Count
            && !IsExpandedParamsCall(method, arguments, model, cancellationToken)
            && model.GetTypeInfo(lambda, cancellationToken).ConvertedType is INamedTypeSymbol { TypeKind: TypeKind.Delegate } target
            && ReturnTypeSurvivesTheConversion(method, target, model.Compilation)
            && ParametersSurviveTheConversion(method, target, model.Compilation);

    /// <summary>Returns whether the delegate's parameters reach the method's without a widening step.</summary>
    /// <param name="method">The method the lambda forwards to.</param>
    /// <param name="target">The delegate type the lambda converts to.</param>
    /// <param name="compilation">The compilation, used to classify the conversion.</param>
    /// <returns><see langword="true"/> when a method group would bind to the delegate.</returns>
    /// <remarks>
    /// A lambda may convert its arguments on the way through; a method group may not. Method-group
    /// compatibility allows only an identity or an implicit <em>reference</em> conversion from each
    /// delegate parameter to the method's. So <c>value =&gt; list.Add(value)</c>, where the list is a
    /// <c>List&lt;object&gt;</c> and the delegate an <c>Action&lt;int&gt;</c>, cannot become
    /// <c>list.Add</c> — <c>int</c> to <c>object</c> is a boxing conversion, and the method group is
    /// CS0123. The lambda is the only form that compiles, so the rule stays quiet.
    /// </remarks>
    private static bool ParametersSurviveTheConversion(IMethodSymbol method, INamedTypeSymbol target, Compilation compilation)
    {
        if (target.DelegateInvokeMethod is not { } invoke
            || invoke.Parameters.Length != method.Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var source = invoke.Parameters[i];
            var destination = method.Parameters[i];
            if (source.RefKind != destination.RefKind)
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(source.Type, destination.Type))
            {
                continue;
            }

            var conversion = compilation.ClassifyConversion(source.Type, destination.Type);
            if (!conversion.IsIdentity && !conversion.IsReference)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the method's return type is one the delegate can actually hold.</summary>
    /// <param name="method">The method the lambda forwards to.</param>
    /// <param name="target">The delegate type the lambda converts to.</param>
    /// <param name="compilation">The compilation, used to classify the conversion.</param>
    /// <returns><see langword="true"/> when a method group would bind to the delegate.</returns>
    /// <remarks>
    /// A lambda body is free to throw a return value away — <c>error =&gt; source.TrySetException(error)</c>
    /// converts to <c>Action&lt;Exception&gt;</c> even though <c>TrySetException</c> returns
    /// <see langword="bool"/>. A method group has no such licence: it must match the delegate's return
    /// type, and offering one here would hand the reader CS0407. The lambda is the only form that
    /// compiles, so the rule stays quiet.
    /// </remarks>
    private static bool ReturnTypeSurvivesTheConversion(IMethodSymbol method, INamedTypeSymbol target, Compilation compilation)
    {
        if (target.DelegateInvokeMethod is not { } invoke)
        {
            return false;
        }

        var expected = invoke.ReturnType;
        if (SymbolEqualityComparer.Default.Equals(method.ReturnType, expected))
        {
            return true;
        }

        if (expected.SpecialType == SpecialType.System_Void)
        {
            return false;
        }

        var conversion = compilation.ClassifyConversion(method.ReturnType, expected);
        return conversion.IsIdentity || conversion.IsReference;
    }

    /// <summary>Returns whether a call to a <see langword="params"/> method uses its expanded form.</summary>
    /// <param name="method">The resolved target method, with one parameter per argument.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the trailing argument is packed into the params collection.</returns>
    /// <remarks>
    /// With one argument per parameter, only the trailing argument can be expanded; the compiler
    /// converts it to the element type when it does, and to the collection type otherwise.
    /// </remarks>
    private static bool IsExpandedParamsCall(
        IMethodSymbol method,
        in SeparatedSyntaxList<ArgumentSyntax> arguments,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var lastParameter = method.Parameters[method.Parameters.Length - 1];
        if (!lastParameter.IsParams)
        {
            return false;
        }

        var convertedType = model.GetTypeInfo(arguments[arguments.Count - 1].Expression, cancellationToken).ConvertedType;
        return !SymbolEqualityComparer.Default.Equals(convertedType, lastParameter.Type);
    }

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
