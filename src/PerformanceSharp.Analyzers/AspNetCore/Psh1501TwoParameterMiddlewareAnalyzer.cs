// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports inline ASP.NET Core middleware written in the legacy nested-delegate form (PSH1501):
/// <c>app.Use(next =&gt; async context =&gt; { ...; await next(context); })</c>, a
/// <c>Func&lt;RequestDelegate, RequestDelegate&gt;</c> whose returned inner delegate closes over
/// <c>next</c> and is carried as an extra per-request closure. The two-parameter overload
/// <c>app.Use(async (context, next) =&gt; { ...; await next(context); })</c> — a
/// <c>Func&lt;HttpContext, RequestDelegate, Task&gt;</c> — needs no nested delegate and no per-request
/// closure.
/// </summary>
/// <remarks>
/// <para>
/// Only the inline nested-lambda shape is reported: the single argument to <c>Use</c> is a
/// single-parameter lambda whose body returns another lambda or anonymous method (directly, wrapped in
/// parentheses, or through a <c>return</c> in a block body). A middleware that returns <c>next</c>
/// unchanged, a method group, or a pre-built delegate allocates no per-request closure and is left
/// alone. The modern two-parameter lambda binds to a different overload and is never reported.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on <c>Microsoft.AspNetCore.Builder.IApplicationBuilder</c>
/// resolving, so a non-web compilation registers no syntax action. The clean path is a name check, an
/// argument-count check, and a syntactic lambda-shape probe; the semantic model is consulted only once
/// the syntax already matches, to confirm the call binds to the legacy
/// <c>Func&lt;RequestDelegate, RequestDelegate&gt;</c> overload on a builder that implements
/// <c>IApplicationBuilder</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1501TwoParameterMiddlewareAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the middleware registration method.</summary>
    private const string UseMethodName = "Use";

    /// <summary>The simple type name of the delegate factory the legacy overload takes.</summary>
    private const string FuncTypeName = "Func";

    /// <summary>The type-argument count of the legacy <c>Func&lt;RequestDelegate, RequestDelegate&gt;</c> parameter.</summary>
    private const int LegacyMiddlewareFuncArity = 2;

    /// <summary>The metadata name of the middleware pipeline builder gating the rule.</summary>
    private const string ApplicationBuilderMetadataName = "Microsoft.AspNetCore.Builder.IApplicationBuilder";

    /// <summary>The metadata name of the request delegate the legacy overload maps.</summary>
    private const string RequestDelegateMetadataName = "Microsoft.AspNetCore.Http.RequestDelegate";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AspNetCoreRules.PreferTwoParameterMiddleware);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(ApplicationBuilderMetadataName) is not { } applicationBuilderType
                || start.Compilation.GetTypeByMetadataName(RequestDelegateMetadataName) is not { } requestDelegateType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, applicationBuilderType, requestDelegateType),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation calls a member named <c>Use</c>, without binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the invoked member is spelled <c>Use</c>.</returns>
    internal static bool IsUseInvocation(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: UseMethodName } => true,
        MemberBindingExpressionSyntax { Name.Identifier.ValueText: UseMethodName } => true,
        _ => false,
    };

    /// <summary>Returns the legacy single-parameter middleware lambda when the argument spells one, without binding.</summary>
    /// <param name="argument">The single argument to <c>Use</c>.</param>
    /// <returns>The nested-delegate lambda to report, or <see langword="null"/> when the shape does not match.</returns>
    internal static LambdaExpressionSyntax? GetLegacyMiddlewareLambda(ArgumentSyntax argument)
    {
        var expression = Unwrap(argument.Expression);
        var isSingleParameterLambda = expression switch
        {
            SimpleLambdaExpressionSyntax => true,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } => true,
            _ => false,
        };

        if (!isSingleParameterLambda)
        {
            return null;
        }

        var lambda = (LambdaExpressionSyntax)expression;
        return BodyReturnsDelegate(lambda.Body) ? lambda : null;
    }

    /// <summary>Reports PSH1501 for a legacy nested-delegate middleware registration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="applicationBuilderType">The gated middleware builder interface.</param>
    /// <param name="requestDelegateType">The gated request delegate type.</param>
    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol applicationBuilderType,
        INamedTypeSymbol requestDelegateType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsUseInvocation(invocation)
            || invocation.ArgumentList.Arguments.Count != 1
            || GetLegacyMiddlewareLambda(invocation.ArgumentList.Arguments[0]) is not { } lambda)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol { Name: UseMethodName, Parameters.Length: 1, ContainingType: { } containingType } method
            || !ImplementsApplicationBuilder(containingType, applicationBuilderType)
            || !IsLegacyMiddlewareParameter(method.Parameters[0].Type, requestDelegateType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AspNetCoreRules.PreferTwoParameterMiddleware,
            lambda.SyntaxTree,
            lambda.Span));
    }

    /// <summary>Unwraps any enclosing parentheses around an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Returns whether a lambda body yields a freshly written delegate.</summary>
    /// <param name="body">The lambda body: an expression or a block.</param>
    /// <returns><see langword="true"/> when the body returns a lambda or anonymous method.</returns>
    private static bool BodyReturnsDelegate(CSharpSyntaxNode body)
    {
        // A lambda body is exhaustively an expression or a block; the block case returns first,
        // so the remaining node is always the expression body.
        if (body is BlockSyntax block)
        {
            for (var i = 0; i < block.Statements.Count; i++)
            {
                if (block.Statements[i] is ReturnStatementSyntax { Expression: { } returned }
                    && IsDelegateExpression(returned))
                {
                    return true;
                }
            }

            return false;
        }

        return IsDelegateExpression((ExpressionSyntax)body);
    }

    /// <summary>Returns whether an expression is a lambda or anonymous method, ignoring parentheses.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> when the expression is a freshly written delegate.</returns>
    private static bool IsDelegateExpression(ExpressionSyntax expression)
        => Unwrap(expression) is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax;

    /// <summary>Returns whether a type is, or implements, the middleware builder interface.</summary>
    /// <param name="type">The method's containing type.</param>
    /// <param name="applicationBuilderType">The gated middleware builder interface.</param>
    /// <returns><see langword="true"/> when the call sits on an <c>IApplicationBuilder</c>.</returns>
    private static bool ImplementsApplicationBuilder(INamedTypeSymbol type, INamedTypeSymbol applicationBuilderType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, applicationBuilderType))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], applicationBuilderType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a parameter type is the legacy <c>Func&lt;RequestDelegate, RequestDelegate&gt;</c>.</summary>
    /// <param name="parameterType">The single parameter type of the bound <c>Use</c> overload.</param>
    /// <param name="requestDelegateType">The gated request delegate type.</param>
    /// <returns><see langword="true"/> when the parameter is the legacy middleware factory delegate.</returns>
    private static bool IsLegacyMiddlewareParameter(ITypeSymbol parameterType, INamedTypeSymbol requestDelegateType)
        => parameterType is INamedTypeSymbol { Name: FuncTypeName, TypeArguments.Length: LegacyMiddlewareFuncArity, ContainingNamespace: { Name: "System" } namespaceSymbol } func
            && namespaceSymbol.ContainingNamespace is { IsGlobalNamespace: true }
            && SymbolEqualityComparer.Default.Equals(func.TypeArguments[0], requestDelegateType)
            && SymbolEqualityComparer.Default.Equals(func.TypeArguments[1], requestDelegateType);
}
