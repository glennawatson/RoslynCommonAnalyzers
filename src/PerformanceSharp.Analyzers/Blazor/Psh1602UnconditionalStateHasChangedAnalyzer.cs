// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a <c>StateHasChanged()</c> call that a component's <c>OnAfterRender</c>/<c>OnAfterRenderAsync</c>
/// reaches unconditionally (PSH1602). Because that callback runs after every render, an unguarded
/// <c>StateHasChanged()</c> schedules another render whose callback calls it again — a render loop that
/// pins the CPU and, on Interactive Server, floods the SignalR circuit.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on <c>Microsoft.AspNetCore.Components.ComponentBase</c>
/// resolving; a project that does not reference Blazor registers no syntax action. On the clean path a
/// candidate invocation fails fast on syntax — its invoked member must be named <c>StateHasChanged</c>
/// (bare or on <c>this</c>), its nearest enclosing method (reached without crossing a lambda or local
/// function) must be named <c>OnAfterRender</c>/<c>OnAfterRenderAsync</c> with a single parameter, and the
/// call must sit on the callback's straight-line path with no intervening <c>if</c> or other branch. Only
/// then is the call bound to <c>ComponentBase.StateHasChanged</c> — a protected member, so binding it there
/// also proves the enclosing type is a component. A call guarded by <c>if (firstRender)</c> or a boolean
/// flag, a call deferred into a lambda, and a same-named call on an unrelated type are never reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1602UnconditionalStateHasChangedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the component base type whose presence proves a Blazor project.</summary>
    private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

    /// <summary>The name of the method that requests another render.</summary>
    private const string StateHasChangedMethodName = "StateHasChanged";

    /// <summary>The name of the synchronous post-render lifecycle callback.</summary>
    private const string OnAfterRenderMethodName = "OnAfterRender";

    /// <summary>The name of the asynchronous post-render lifecycle callback.</summary>
    private const string OnAfterRenderAsyncMethodName = "OnAfterRenderAsync";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(BlazorRules.UnconditionalStateHasChanged);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // The post-render callbacks are hand-written overrides, never generated render code.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var componentBase = start.Compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
            if (componentBase is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, componentBase), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1602 when a component reaches <c>StateHasChanged()</c> unconditionally from a post-render callback.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="componentBase">The resolved component base type gating the rule.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol componentBase)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsStateHasChangedName(invocation.Expression))
        {
            return;
        }

        var callback = FindRenderCallback(invocation);
        if (callback is null || !IsUnconditional(invocation, callback))
        {
            return;
        }

        // 'StateHasChanged' is a protected member of the component base, so binding it there also proves the
        // enclosing type derives from the component base — no separate derivation check is needed.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol called
            || called.Name != StateHasChangedMethodName
            || !DerivesFromOrIs(called.ContainingType, componentBase))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            BlazorRules.UnconditionalStateHasChanged,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Returns whether an invoked expression names <c>StateHasChanged</c> bare or on <c>this</c>.</summary>
    /// <param name="expression">The invocation's expression.</param>
    /// <returns><see langword="true"/> when the call targets a member named <c>StateHasChanged</c>.</returns>
    private static bool IsStateHasChangedName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == StateHasChangedMethodName,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.ValueText == StateHasChangedMethodName,
        _ => false,
    };

    /// <summary>Returns the enclosing post-render lifecycle callback reached without crossing a nested function.</summary>
    /// <param name="node">The node to search up from.</param>
    /// <returns>
    /// The enclosing <c>OnAfterRender</c>/<c>OnAfterRenderAsync</c> method when the call sits directly in its body;
    /// <see langword="null"/> when a nested function is crossed first or the enclosing method is something else.
    /// </returns>
    private static MethodDeclarationSyntax? FindRenderCallback(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    return null;

                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText is OnAfterRenderMethodName or OnAfterRenderAsyncMethodName
                        && method.ParameterList.Parameters.Count == 1
                            ? method
                            : null;

                case MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Returns whether the call sits on the callback's straight-line path with no intervening branch.</summary>
    /// <param name="call">The <c>StateHasChanged()</c> call.</param>
    /// <param name="callback">The enclosing post-render callback.</param>
    /// <returns>
    /// <see langword="true"/> when only statement, block, and expression-body nodes separate the call from the
    /// callback; <see langword="false"/> as soon as an <c>if</c>, loop, <c>switch</c>, or any other branch is crossed.
    /// </returns>
    private static bool IsUnconditional(InvocationExpressionSyntax call, MethodDeclarationSyntax callback)
    {
        for (var current = call.Parent; current is not null && current != callback; current = current.Parent)
        {
            switch (current)
            {
                case ExpressionStatementSyntax:
                case BlockSyntax:
                case ArrowExpressionClauseSyntax:
                    continue;

                default:
                    return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a type is the component base or derives from it.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="componentBase">The resolved component base type.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is or derives from <paramref name="componentBase"/>.</returns>
    private static bool DerivesFromOrIs(ITypeSymbol? type, INamedTypeSymbol componentBase)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, componentBase))
            {
                return true;
            }
        }

        return false;
    }
}
