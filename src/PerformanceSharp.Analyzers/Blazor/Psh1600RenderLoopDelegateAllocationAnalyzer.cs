// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a delegate that is allocated once per iteration inside a component render loop (PSH1600).
/// The reported shape is an anonymous function — a lambda or anonymous method, whether written inline
/// or handed to <c>EventCallback.Factory.Create(...)</c> — that sits directly inside a <c>for</c> or
/// <c>foreach</c> in a component's <c>BuildRenderTree</c> render output and reads a variable declared by
/// that loop (the iteration variable, a <c>for</c> counter, or a local declared in the loop body). Such a
/// delegate captures a value that changes every iteration, so the compiler allocates a distinct closure
/// and delegate for each row, and the whole set is rebuilt on every render.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on
/// <c>Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder</c> resolving; a project that does not
/// reference Blazor registers no syntax action. On the clean path a candidate anonymous function fails
/// fast on syntax: it is discarded unless its nearest enclosing statement (with no intervening anonymous
/// function) is a <c>for</c>/<c>foreach</c> whose containing method is named <c>BuildRenderTree</c> and
/// takes a single parameter, all checked before the semantic model is consulted. The render-method
/// parameter type and the loop-variable capture are bound only once those syntactic gates pass, so a
/// loop-invariant delegate, a method group, and a delegate hoisted out of the loop are never reported.
/// Generated code is analyzed because a <c>.razor</c> component's <c>@foreach</c>/<c>@for</c> render body
/// compiles into exactly this <c>BuildRenderTree</c> shape.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1600RenderLoopDelegateAllocationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the render-tree builder whose presence proves a Blazor project.</summary>
    private const string RenderTreeBuilderMetadataName = "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder";

    /// <summary>The name of the component render method whose body holds the render loops.</summary>
    private const string BuildRenderTreeMethodName = "BuildRenderTree";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(BlazorRules.RenderLoopDelegateAllocation);

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

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeAnonymousFunction(nodeContext, renderTreeBuilder),
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression,
                SyntaxKind.AnonymousMethodExpression);
        });
    }

    /// <summary>Reports PSH1600 when an anonymous function inside a render loop captures a loop-declared variable.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="renderTreeBuilder">The resolved render-tree builder type gating the rule.</param>
    private static void AnalyzeAnonymousFunction(SyntaxNodeAnalysisContext context, INamedTypeSymbol renderTreeBuilder)
    {
        var anonymousFunction = (AnonymousFunctionExpressionSyntax)context.Node;

        var loop = FindEnclosingRenderLoop(anonymousFunction);
        if (loop is null)
        {
            return;
        }

        if (!IsInsideRenderTreeMethod(loop, context.SemanticModel, renderTreeBuilder, context.CancellationToken))
        {
            return;
        }

        if (!CapturesLoopLocal(anonymousFunction, loop, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            BlazorRules.RenderLoopDelegateAllocation,
            anonymousFunction.SyntaxTree,
            anonymousFunction.Span));
    }

    /// <summary>Returns the nearest enclosing <c>for</c>/<c>foreach</c> reached without crossing another function.</summary>
    /// <param name="anonymousFunction">The candidate anonymous function.</param>
    /// <returns>
    /// The enclosing loop when the anonymous function sits directly in a loop body; <see langword="null"/> when a
    /// nested function is crossed first (the outer function owns the per-iteration cost) or no loop encloses it.
    /// </returns>
    private static SyntaxNode? FindEnclosingRenderLoop(AnonymousFunctionExpressionSyntax anonymousFunction)
    {
        for (var current = anonymousFunction.Parent; current is not null; current = current.Parent)
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

    /// <summary>Returns whether the anonymous function reads a variable declared inside the loop.</summary>
    /// <param name="anonymousFunction">The candidate anonymous function.</param>
    /// <param name="loop">The enclosing loop.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <returns><see langword="true"/> when the delegate captures the iteration variable or a loop-body local.</returns>
    private static bool CapturesLoopLocal(AnonymousFunctionExpressionSyntax anonymousFunction, SyntaxNode loop, SemanticModel semanticModel)
    {
        var body = anonymousFunction.Body;
        var dataFlow = body is ExpressionSyntax expression
            ? semanticModel.AnalyzeDataFlow(expression)
            : semanticModel.AnalyzeDataFlow((StatementSyntax)body);

        if (dataFlow is not { Succeeded: true })
        {
            return false;
        }

        var readInside = dataFlow.ReadInside;
        for (var i = 0; i < readInside.Length; i++)
        {
            if (readInside[i] is ILocalSymbol local && IsDeclaredWithin(local, loop))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a local's declaration lies within the loop's span.</summary>
    /// <param name="local">The read local symbol.</param>
    /// <param name="loop">The enclosing loop.</param>
    /// <returns><see langword="true"/> when the local is declared inside the loop (so it varies per iteration).</returns>
    private static bool IsDeclaredWithin(ILocalSymbol local, SyntaxNode loop)
    {
        var references = local.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            var reference = references[i];
            if (reference.SyntaxTree == loop.SyntaxTree && loop.Span.Contains(reference.Span))
            {
                return true;
            }
        }

        return false;
    }
}
