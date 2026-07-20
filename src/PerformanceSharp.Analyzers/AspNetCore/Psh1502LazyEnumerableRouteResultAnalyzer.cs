// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a web route handler that returns a deferred sequence (PSH1502). Two shapes are covered: a
/// route-handler lambda passed to a <c>Microsoft.AspNetCore.Builder</c> map method
/// (<c>MapGet</c>/<c>MapPost</c>/<c>MapPut</c>/<c>MapDelete</c>/<c>MapPatch</c>/<c>Map</c>), and a public
/// instance action method on a <c>Microsoft.AspNetCore.Mvc.ControllerBase</c> subclass. In both cases the
/// handler's result must be typed exactly as <c>IEnumerable&lt;T&gt;</c> (or <c>Task&lt;IEnumerable&lt;T&gt;&gt;</c> /
/// <c>ValueTask&lt;IEnumerable&lt;T&gt;&gt;</c>), and the returned expression must bind to a deferred query — an
/// <c>IQueryable&lt;T&gt;</c> or a lazy <c>System.Linq</c> operator result — so the report stays near-zero
/// false positive and never fires on an already-materialized collection.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on <c>Microsoft.AspNetCore.Builder.WebApplication</c> or
/// <c>Microsoft.AspNetCore.Mvc.ControllerBase</c> resolving; a project that references neither registers no
/// syntax action. The clean path fails fast on a syntactic prefilter (a map-method name with a lambda
/// argument, or a public generic-returning method) before the semantic model is consulted, and the deferred
/// expression is bound only once the return type gate has already passed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1502LazyEnumerableRouteResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the minimal-API host whose presence proves an ASP.NET Core web project.</summary>
    private const string WebApplicationMetadataName = "Microsoft.AspNetCore.Builder.WebApplication";

    /// <summary>The metadata name of the MVC controller base class actions derive from.</summary>
    private const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";

    /// <summary>The metadata name of the non-generic queryable marker every <c>IQueryable&lt;T&gt;</c> implements.</summary>
    private const string QueryableMarkerMetadataName = "System.Linq.IQueryable";

    /// <summary>The metadata name of the in-memory LINQ operator holder.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <summary>The metadata name of the expression-tree LINQ operator holder.</summary>
    private const string QueryableMetadataName = "System.Linq.Queryable";

    /// <summary>The metadata name of the awaitable task wrapper.</summary>
    private const string TaskMetadataName = "System.Threading.Tasks.Task`1";

    /// <summary>The metadata name of the awaitable value-task wrapper.</summary>
    private const string ValueTaskMetadataName = "System.Threading.Tasks.ValueTask`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AspNetCoreRules.LazyEnumerableRouteResult);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            var webApplication = compilation.GetTypeByMetadataName(WebApplicationMetadataName);
            var controllerBase = compilation.GetTypeByMetadataName(ControllerBaseMetadataName);
            if (webApplication is null && controllerBase is null)
            {
                return;
            }

            var model = new DeferredResultModel(
                controllerBase,
                compilation.GetTypeByMetadataName(QueryableMarkerMetadataName),
                compilation.GetTypeByMetadataName(EnumerableMetadataName),
                compilation.GetTypeByMetadataName(QueryableMetadataName),
                compilation.GetTypeByMetadataName(TaskMetadataName),
                compilation.GetTypeByMetadataName(ValueTaskMetadataName));

            if (webApplication is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMapInvocation(nodeContext, model), SyntaxKind.InvocationExpression);
            }

            if (controllerBase is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAction(nodeContext, model), SyntaxKind.MethodDeclaration);
            }
        });
    }

    /// <summary>Returns whether a simple name is one of the route-handler map methods.</summary>
    /// <param name="name">The invoked member's simple name.</param>
    /// <returns><see langword="true"/> when the name maps an endpoint route handler.</returns>
    internal static bool IsMapMethodName(string name) => name switch
    {
        "MapGet" or "MapPost" or "MapPut" or "MapDelete" or "MapPatch" or "Map" => true,
        _ => false,
    };

    /// <summary>Returns whether a LINQ operator name produces a deferred sequence.</summary>
    /// <param name="name">The operator method's name.</param>
    /// <returns><see langword="true"/> for the filter/projection/order operators that iterate lazily.</returns>
    internal static bool IsDeferredOperatorName(string name) => name switch
    {
        "Where" or "Select" or "SelectMany" or "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" => true,
        _ => false,
    };

    /// <summary>Reports PSH1502 for a route-handler lambda passed to an ASP.NET Core map method.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    private static void AnalyzeMapInvocation(SyntaxNodeAnalysisContext context, DeferredResultModel model)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !IsMapMethodName(memberAccess.Name.Identifier.ValueText))
        {
            return;
        }

        var lambda = FindLambdaArgument(invocation.ArgumentList);
        if (lambda is null)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType.ContainingNamespace: { } mapNamespace }
            || !IsRoutingNamespace(mapNamespace))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(lambda, context.CancellationToken).Symbol is not IMethodSymbol lambdaSymbol
            || !IsLazyEnumerableReturnType(lambdaSymbol.ReturnType, model))
        {
            return;
        }

        AnalyzeReturns(context, lambda.Body, model);
    }

    /// <summary>Reports PSH1502 for a public action method on a controller that returns a deferred sequence.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    private static void AnalyzeAction(SyntaxNodeAnalysisContext context, DeferredResultModel model)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!IsCandidateActionShape(method))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol methodSymbol
            || methodSymbol.DeclaredAccessibility != Accessibility.Public
            || methodSymbol.IsStatic
            || !DerivesFromControllerBase(methodSymbol.ContainingType, model.ControllerBaseType)
            || !IsLazyEnumerableReturnType(methodSymbol.ReturnType, model))
        {
            return;
        }

        if (method.ExpressionBody is { } arrow)
        {
            TryReportDeferred(context, arrow.Expression, model);
            return;
        }

        if (method.Body is not { } body)
        {
            return;
        }

        AnalyzeReturnStatements(context, body, model);
    }

    /// <summary>Analyzes a lambda body, whether it is an expression or a block of return statements.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="body">The lambda body.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    private static void AnalyzeReturns(SyntaxNodeAnalysisContext context, CSharpSyntaxNode body, DeferredResultModel model)
    {
        if (body is ExpressionSyntax expression)
        {
            TryReportDeferred(context, expression, model);
            return;
        }

        if (body is not BlockSyntax block)
        {
            return;
        }

        AnalyzeReturnStatements(context, block, model);
    }

    /// <summary>Reports every deferred returned expression in a block, skipping nested function bodies.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The block (or nested statement) to scan.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    private static void AnalyzeReturnStatements(SyntaxNodeAnalysisContext context, SyntaxNode node, DeferredResultModel model)
    {
        foreach (var child in node.ChildNodes())
        {
            switch (child)
            {
                case ReturnStatementSyntax { Expression: { } returned }:
                {
                    TryReportDeferred(context, returned, model);
                    break;
                }

                case SimpleLambdaExpressionSyntax:
                case ParenthesizedLambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    break;

                default:
                {
                    AnalyzeReturnStatements(context, child, model);
                    break;
                }
            }
        }
    }

    /// <summary>Reports PSH1502 when a returned expression binds to a deferred query.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The returned expression to classify.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    private static void TryReportDeferred(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, DeferredResultModel model)
    {
        if (!IsDeferredSequence(context.SemanticModel, expression, model, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AspNetCoreRules.LazyEnumerableRouteResult,
            expression.SyntaxTree,
            expression.Span));
    }

    /// <summary>Returns the first lambda handed to a map invocation, if any.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <returns>The route-handler lambda, or <see langword="null"/> when no argument is a lambda.</returns>
    private static LambdaExpressionSyntax? FindLambdaArgument(ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].Expression is LambdaExpressionSyntax lambda)
            {
                return lambda;
            }
        }

        return null;
    }

    /// <summary>Returns whether a method declaration could be a public instance action before binding.</summary>
    /// <param name="method">The method declaration to prefilter.</param>
    /// <returns><see langword="true"/> when the syntax looks like a candidate action.</returns>
    private static bool IsCandidateActionShape(MethodDeclarationSyntax method)
    {
        var hasPublic = false;
        var modifiers = method.Modifiers;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind is SyntaxKind.StaticKeyword or SyntaxKind.AbstractKeyword)
            {
                return false;
            }

            if (kind == SyntaxKind.PublicKeyword)
            {
                hasPublic = true;
            }
        }

        if (!hasPublic || (method.ExpressionBody is null && method.Body is null) || HasNonActionAttribute(method))
        {
            return false;
        }

        return IsCandidateReturnTypeSyntax(method.ReturnType);
    }

    /// <summary>Returns whether the written return type could unwrap to <c>IEnumerable&lt;T&gt;</c>.</summary>
    /// <param name="type">The written return type.</param>
    /// <returns><see langword="true"/> when the outermost simple name is a generic enumerable or task wrapper.</returns>
    private static bool IsCandidateReturnTypeSyntax(TypeSyntax type)
    {
        var name = GetOutermostGenericName(type);
        return name switch
        {
            "IEnumerable" or "Task" or "ValueTask" => true,
            _ => false,
        };
    }

    /// <summary>Returns the identifier of the outermost generic name in a type syntax, if it is generic.</summary>
    /// <param name="type">The written type.</param>
    /// <returns>The outermost generic identifier, or <see langword="null"/> when the type is not generic.</returns>
    private static string? GetOutermostGenericName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetOutermostGenericName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetOutermostGenericName(alias.Name),
        _ => null,
    };

    /// <summary>Returns whether a method carries an attribute that excludes it from being an action.</summary>
    /// <param name="method">The method declaration to inspect.</param>
    /// <returns><see langword="true"/> when a <c>NonAction</c> attribute is written on the method.</returns>
    private static bool HasNonActionAttribute(MethodDeclarationSyntax method)
    {
        var lists = method.AttributeLists;
        for (var i = 0; i < lists.Count; i++)
        {
            var attributes = lists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var name = GetSimpleAttributeName(attributes[j].Name);
                if (name is "NonAction" or "NonActionAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns the rightmost identifier of a written attribute name.</summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when the syntax names no simple identifier.</returns>
    private static string? GetSimpleAttributeName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetSimpleAttributeName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetSimpleAttributeName(alias.Name),
        _ => null,
    };

    /// <summary>Returns whether a namespace is <c>Microsoft.AspNetCore.Builder</c>.</summary>
    /// <param name="ns">The namespace to test.</param>
    /// <returns><see langword="true"/> when the namespace is the ASP.NET Core routing namespace.</returns>
    private static bool IsRoutingNamespace(INamespaceSymbol ns)
        => ns is { Name: "Builder" }
            && ns.ContainingNamespace is { Name: "AspNetCore" } aspNetCore
            && aspNetCore.ContainingNamespace is { Name: "Microsoft" } microsoft
            && microsoft.ContainingNamespace is { IsGlobalNamespace: true };

    /// <summary>Returns whether a type derives from (or is) the MVC controller base.</summary>
    /// <param name="type">The containing type of the analyzed method.</param>
    /// <param name="controllerBase">The resolved controller base type; always non-null while the action analysis is registered.</param>
    /// <returns><see langword="true"/> when the type is a controller.</returns>
    private static bool DerivesFromControllerBase(INamedTypeSymbol? type, INamedTypeSymbol? controllerBase)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, controllerBase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a return type, after unwrapping a task wrapper, is exactly <c>IEnumerable&lt;T&gt;</c>.</summary>
    /// <param name="returnType">The declared or inferred return type.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    /// <returns><see langword="true"/> for <c>IEnumerable&lt;T&gt;</c>, <c>Task&lt;IEnumerable&lt;T&gt;&gt;</c>, or the value-task form.</returns>
    private static bool IsLazyEnumerableReturnType(ITypeSymbol returnType, DeferredResultModel model)
    {
        var unwrapped = UnwrapTaskLike(returnType, model);
        return unwrapped is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
    }

    /// <summary>Unwraps a single <c>Task&lt;T&gt;</c> or <c>ValueTask&lt;T&gt;</c> layer.</summary>
    /// <param name="type">The type to unwrap.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    /// <returns>The awaited element type, or the input type when it is not a task wrapper.</returns>
    private static ITypeSymbol UnwrapTaskLike(ITypeSymbol type, DeferredResultModel model)
    {
        if (type is INamedTypeSymbol { TypeArguments.Length: 1 } named
            && (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, model.TaskOfT)
                || SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, model.ValueTaskOfT)))
        {
            return named.TypeArguments[0];
        }

        return type;
    }

    /// <summary>Returns whether a returned expression binds to a deferred query.</summary>
    /// <param name="semanticModel">The semantic model.</param>
    /// <param name="expression">The returned expression.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the value is an <c>IQueryable&lt;T&gt;</c> or a lazy LINQ operator result.</returns>
    private static bool IsDeferredSequence(SemanticModel semanticModel, ExpressionSyntax expression, DeferredResultModel model, CancellationToken cancellationToken)
    {
        var type = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
        if (type is not null && model.QueryableMarker is not null && ImplementsQueryable(type, model.QueryableMarker))
        {
            return true;
        }

        return expression is InvocationExpressionSyntax invocation
            && semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && IsDeferredLinqOperator(method, model);
    }

    /// <summary>Returns whether a type is, or implements, the non-generic queryable marker.</summary>
    /// <param name="type">The value's type.</param>
    /// <param name="queryableMarker">The resolved <c>System.Linq.IQueryable</c> marker.</param>
    /// <returns><see langword="true"/> when the value is a queryable.</returns>
    private static bool ImplementsQueryable(ITypeSymbol type, INamedTypeSymbol queryableMarker)
    {
        if (SymbolEqualityComparer.Default.Equals(type, queryableMarker))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], queryableMarker))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a bound method is a deferred <c>System.Linq</c> operator.</summary>
    /// <param name="method">The bound invocation method.</param>
    /// <param name="model">The compilation's resolved deferred-sequence types.</param>
    /// <returns><see langword="true"/> when the method is a deferred operator on the LINQ operator holders.</returns>
    private static bool IsDeferredLinqOperator(IMethodSymbol method, DeferredResultModel model)
    {
        var containingType = (method.ReducedFrom ?? method).ContainingType;
        return (SymbolEqualityComparer.Default.Equals(containingType, model.EnumerableType)
                || SymbolEqualityComparer.Default.Equals(containingType, model.QueryableType))
            && IsDeferredOperatorName(method.Name);
    }

    /// <summary>Holds the types PSH1502 resolves once per compilation to classify a deferred return.</summary>
    private sealed class DeferredResultModel
    {
        /// <summary>Initializes a new instance of the <see cref="DeferredResultModel"/> class.</summary>
        /// <param name="controllerBaseType">The resolved MVC controller base, when referenced.</param>
        /// <param name="queryableMarker">The resolved non-generic queryable marker, when referenced.</param>
        /// <param name="enumerableType">The resolved in-memory LINQ operator holder, when referenced.</param>
        /// <param name="queryableType">The resolved expression-tree LINQ operator holder, when referenced.</param>
        /// <param name="taskOfT">The resolved <c>Task&lt;T&gt;</c> definition, when referenced.</param>
        /// <param name="valueTaskOfT">The resolved <c>ValueTask&lt;T&gt;</c> definition, when referenced.</param>
        internal DeferredResultModel(
            INamedTypeSymbol? controllerBaseType,
            INamedTypeSymbol? queryableMarker,
            INamedTypeSymbol? enumerableType,
            INamedTypeSymbol? queryableType,
            INamedTypeSymbol? taskOfT,
            INamedTypeSymbol? valueTaskOfT)
        {
            ControllerBaseType = controllerBaseType;
            QueryableMarker = queryableMarker;
            EnumerableType = enumerableType;
            QueryableType = queryableType;
            TaskOfT = taskOfT;
            ValueTaskOfT = valueTaskOfT;
        }

        /// <summary>Gets the resolved MVC controller base, when referenced.</summary>
        internal INamedTypeSymbol? ControllerBaseType { get; }

        /// <summary>Gets the resolved non-generic queryable marker, when referenced.</summary>
        internal INamedTypeSymbol? QueryableMarker { get; }

        /// <summary>Gets the resolved in-memory LINQ operator holder, when referenced.</summary>
        internal INamedTypeSymbol? EnumerableType { get; }

        /// <summary>Gets the resolved expression-tree LINQ operator holder, when referenced.</summary>
        internal INamedTypeSymbol? QueryableType { get; }

        /// <summary>Gets the resolved <c>Task&lt;T&gt;</c> definition, when referenced.</summary>
        internal INamedTypeSymbol? TaskOfT { get; }

        /// <summary>Gets the resolved <c>ValueTask&lt;T&gt;</c> definition, when referenced.</summary>
        internal INamedTypeSymbol? ValueTaskOfT { get; }
    }
}
