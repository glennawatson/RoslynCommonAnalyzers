// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a non-delegate allocation handed to a child component as a parameter value inside a
/// <c>for</c>/<c>foreach</c> render loop (PSH1603). The reported shape is a <c>new</c> object, an array or
/// collection literal, or a materializing query (<c>ToList</c>/<c>ToArray</c>/…) passed as the value of a
/// <c>RenderTreeBuilder.AddComponentParameter(...)</c> call directly inside a loop in a component's
/// <c>BuildRenderTree</c>. Each iteration allocates a fresh instance on every render, and the new reference
/// makes the child treat its parameter as changed and re-render. This is the non-delegate sibling of
/// PSH1600, which owns the delegate case.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on
/// <c>Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder</c> resolving; a project that does not
/// reference Blazor registers no syntax action. On the clean path a candidate invocation fails fast on
/// syntax — its invoked member must be named <c>AddComponentParameter</c> with at least three arguments,
/// its value argument must be an allocation syntax, and its nearest enclosing statement (reached without
/// crossing a lambda or local function) must be a <c>for</c>/<c>foreach</c> — before any binding. The
/// containing method is confirmed to be <c>BuildRenderTree(RenderTreeBuilder)</c>, the receiver is
/// confirmed to be the render-tree builder, and a <c>new</c> value's type is checked so a delegate creation
/// is left to PSH1600. Generated code is analyzed because a <c>.razor</c> component's
/// <c>@foreach</c>/<c>@for</c> body compiles into exactly this <c>AddComponentParameter</c> shape.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1603RenderLoopParameterAllocationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the render-tree builder whose presence proves a Blazor project.</summary>
    private const string RenderTreeBuilderMetadataName = "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder";

    /// <summary>The metadata name of the query type whose materializing calls allocate a fresh collection.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <summary>The name of the builder method that sets a child component's parameter.</summary>
    private const string AddComponentParameterMethodName = "AddComponentParameter";

    /// <summary>The name of the component render method whose body holds the render loops.</summary>
    private const string BuildRenderTreeMethodName = "BuildRenderTree";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(BlazorRules.RenderLoopParameterAllocation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A .razor component's render body is generated code, so the rule must both analyze it and report there.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(static start =>
        {
            var renderTreeBuilder = start.Compilation.GetTypeByMetadataName(RenderTreeBuilderMetadataName);
            if (renderTreeBuilder is null)
            {
                return;
            }

            var gate = new AllocationGate(renderTreeBuilder, start.Compilation.GetTypeByMetadataName(EnumerableMetadataName));
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, gate), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1603 when a component-parameter value inside a render loop is a non-delegate allocation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="gate">The resolved render-tree builder and query types gating the rule.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, AllocationGate gate)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != AddComponentParameterMethodName)
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 3)
        {
            return;
        }

        var value = arguments[2].Expression;
        if (!IsAllocationSyntax(value))
        {
            return;
        }

        var loop = FindEnclosingRenderLoop(invocation);
        if (loop is null || !IsInsideRenderTreeMethod(loop, context.SemanticModel, gate.RenderTreeBuilder, context.CancellationToken))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (receiverType is null || !SymbolEqualityComparer.Default.Equals(receiverType, gate.RenderTreeBuilder))
        {
            return;
        }

        if (!IsNonDelegateAllocation(value, context.SemanticModel, gate.Enumerable, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            BlazorRules.RenderLoopParameterAllocation,
            value.SyntaxTree,
            value.Span));
    }

    /// <summary>Returns whether an expression is syntactically an allocation worth binding.</summary>
    /// <param name="value">The component-parameter value expression.</param>
    /// <returns><see langword="true"/> for object/array/collection creations and materializing query calls.</returns>
    private static bool IsAllocationSyntax(ExpressionSyntax value) => Unwrap(value) switch
    {
        ObjectCreationExpressionSyntax
            or ImplicitObjectCreationExpressionSyntax
            or ArrayCreationExpressionSyntax
            or ImplicitArrayCreationExpressionSyntax => true,
        InvocationExpressionSyntax invocation => IsMaterializingCallName(invocation),
        _ => false,
    };

    /// <summary>Returns whether an invocation names a collection-materializing method.</summary>
    /// <param name="invocation">The candidate call.</param>
    /// <returns><see langword="true"/> when the invoked member is a <c>To*</c> materializer.</returns>
    private static bool IsMaterializingCallName(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText switch
        {
            "ToList" or "ToArray" or "ToHashSet" or "ToDictionary" or "ToLookup" => true,
            _ => false,
        };

    /// <summary>Returns whether an allocation value is a non-delegate allocation (a delegate is PSH1600's concern).</summary>
    /// <param name="value">The component-parameter value expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="enumerable">The resolved query type, when present.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the value allocates and is not a delegate.</returns>
    private static bool IsNonDelegateAllocation(ExpressionSyntax value, SemanticModel model, INamedTypeSymbol? enumerable, CancellationToken cancellationToken)
    {
        var expression = Unwrap(value);
        return expression switch
        {
            ArrayCreationExpressionSyntax
                or ImplicitArrayCreationExpressionSyntax => true,
            ObjectCreationExpressionSyntax
                or ImplicitObjectCreationExpressionSyntax => model.GetTypeInfo(expression, cancellationToken).Type is { TypeKind: not TypeKind.Delegate },
            InvocationExpressionSyntax invocation => enumerable is not null
                && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
                && SymbolEqualityComparer.Default.Equals(method.ContainingType, enumerable),
            _ => false,
        };
    }

    /// <summary>Returns the nearest enclosing <c>for</c>/<c>foreach</c> reached without crossing another function.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns>The enclosing loop, or <see langword="null"/> when a nested function is crossed first or no loop encloses it.</returns>
    private static SyntaxNode? FindEnclosingRenderLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    return null;

                case ForStatementSyntax:
                case CommonForEachStatementSyntax:
                    return current;

                case MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Returns whether a loop's containing method is a component <c>BuildRenderTree</c> override.</summary>
    /// <param name="loop">The enclosing loop.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <param name="renderTreeBuilder">The resolved render-tree builder type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the loop runs inside a <c>BuildRenderTree(RenderTreeBuilder)</c> method.</returns>
    private static bool IsInsideRenderTreeMethod(SyntaxNode loop, SemanticModel semanticModel, INamedTypeSymbol renderTreeBuilder, CancellationToken cancellationToken)
    {
        var method = FindContainingMethod(loop);
        if (method is null
            || method.Identifier.ValueText != BuildRenderTreeMethodName
            || method.ParameterList.Parameters.Count != 1)
        {
            return false;
        }

        return semanticModel.GetDeclaredSymbol(method, cancellationToken) is IMethodSymbol { Parameters.Length: 1 } symbol
            && SymbolEqualityComparer.Default.Equals(symbol.Parameters[0].Type, renderTreeBuilder);
    }

    /// <summary>Returns the nearest enclosing method declaration, walking through any nested functions.</summary>
    /// <param name="node">The node to search up from.</param>
    /// <returns>The containing method declaration, or <see langword="null"/> when the node is not inside a method.</returns>
    private static MethodDeclarationSyntax? FindContainingMethod(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax method:
                    return method;

                case BaseTypeDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Peels parentheses off an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized.Expression;
        }

        return current;
    }

    /// <summary>The render-tree builder and query types resolved once per compilation.</summary>
    /// <param name="RenderTreeBuilder">The render-tree builder type; always present while the rule is registered.</param>
    /// <param name="Enumerable">The query type whose materializers allocate, when the framework exposes one.</param>
    private readonly record struct AllocationGate(INamedTypeSymbol RenderTreeBuilder, INamedTypeSymbol? Enumerable);
}
