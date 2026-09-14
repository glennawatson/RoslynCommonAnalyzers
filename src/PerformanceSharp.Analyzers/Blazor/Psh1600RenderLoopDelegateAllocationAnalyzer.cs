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
/// The rule resolves <c>Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder</c> on first demand,
/// caching the result per compilation even when the type is absent. On the clean path a candidate anonymous function fails
/// fast on syntax: it is discarded unless its nearest enclosing statement (with no intervening anonymous
/// function) is a <c>for</c>/<c>foreach</c> whose containing method is named <c>BuildRenderTree</c> and
/// takes a single parameter, all checked before type resolution or the semantic model is consulted. The render-method
/// parameter type and the loop-variable capture are bound only once those syntactic gates pass, so a
/// loop-invariant delegate, a method group, and a delegate hoisted out of the loop are never reported.
/// Generated code is analyzed because a <c>.razor</c> component's <c>@foreach</c>/<c>@for</c> render body
/// compiles into exactly this <c>BuildRenderTree</c> shape.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1600RenderLoopDelegateAllocationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the component render method whose body holds the render loops.</summary>
    private const string BuildRenderTreeMethodName = "BuildRenderTree";

    /// <summary>The metadata name of the render-tree builder whose presence proves a Blazor project.</summary>
    private const string RenderTreeBuilderMetadataName = "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(BlazorRules.RenderLoopDelegateAllocation);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A .razor component's render body is generated code, so the rule must both analyze it and report there.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, RenderTreeBuilderMetadataName),
            AnalyzeAnonymousFunction,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression);
    }

    /// <summary>Reports PSH1600 when an anonymous function inside a render loop captures a loop-declared variable.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The lazily resolved render-tree builder type gating the rule.</param>
    private static void AnalyzeAnonymousFunction(in SyntaxNodeAnalysisContext context, LazyMetadataType types)
    {
        var anonymousFunction = (AnonymousFunctionExpressionSyntax)context.Node;

        var loop = RenderLoopSyntax.FindEnclosingLoop(anonymousFunction);
        if (loop is null)
        {
            return;
        }

        if (!IsInsideRenderTreeMethod(loop, context.SemanticModel, types, context.CancellationToken))
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

    /// <summary>Returns whether a loop's containing method is a component <c>BuildRenderTree</c> override.</summary>
    /// <param name="loop">The enclosing loop.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <param name="types">The lazily resolved render-tree builder type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the loop runs inside a <c>BuildRenderTree(RenderTreeBuilder)</c> method.</returns>
    private static bool IsInsideRenderTreeMethod(SyntaxNode loop, SemanticModel semanticModel, LazyMetadataType types, CancellationToken cancellationToken)
    {
        var method = RenderLoopSyntax.FindContainingMethod(loop);
        if (method is null
            || method.Identifier.ValueText != BuildRenderTreeMethodName
            || method.ParameterList.Parameters.Count != 1)
        {
            return false;
        }

        return types.Get() is { } renderTreeBuilder
            && semanticModel.GetDeclaredSymbol(method, cancellationToken) is IMethodSymbol { Parameters.Length: 1 } symbol
            && SymbolEqualityComparer.Default.Equals(symbol.Parameters[0].Type, renderTreeBuilder);
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
