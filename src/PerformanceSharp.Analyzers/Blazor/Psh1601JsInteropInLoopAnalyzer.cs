// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a JavaScript-interop call issued once per iteration inside a <c>for</c>/<c>foreach</c>
/// (PSH1601). The reported shape is an <c>InvokeAsync</c>/<c>InvokeVoidAsync</c> call whose receiver is
/// an <c>IJSRuntime</c> or <c>IJSObjectReference</c>, written directly in a loop body. Under the
/// Interactive Server hosting model each such call is a separate SignalR round-trip to the browser, so a
/// collection of N items becomes N network hops instead of one batched call.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on <c>Microsoft.JSInterop.IJSRuntime</c> resolving; a
/// project that does not reference Blazor registers no syntax action. On the clean path a candidate
/// invocation fails fast on syntax — its invoked member must be named <c>InvokeAsync</c> or
/// <c>InvokeVoidAsync</c>, and its nearest enclosing statement (reached without crossing a lambda or
/// local function) must be a <c>for</c>/<c>foreach</c> — before the receiver type is bound. The receiver
/// is confirmed to be the JavaScript-runtime or object-reference interface only once those syntactic
/// gates pass, so a call outside a loop, or a same-named call on an unrelated receiver, is never
/// reported. A call nested inside a lambda or local function is left to that function's own analysis.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1601JsInteropInLoopAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the JavaScript-runtime interface whose presence proves a Blazor project.</summary>
    private const string JsRuntimeMetadataName = "Microsoft.JSInterop.IJSRuntime";

    /// <summary>The metadata name of the JavaScript object-reference interface whose interop calls are also watched.</summary>
    private const string JsObjectReferenceMetadataName = "Microsoft.JSInterop.IJSObjectReference";

    /// <summary>The name of the value-returning interop method.</summary>
    private const string InvokeAsyncMethodName = "InvokeAsync";

    /// <summary>The name of the void interop method.</summary>
    private const string InvokeVoidAsyncMethodName = "InvokeVoidAsync";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(BlazorRules.JsInteropInLoop);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A .razor component can host interop in a generated render body, so analyze and report there too.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(static start =>
        {
            var jsRuntime = start.Compilation.GetTypeByMetadataName(JsRuntimeMetadataName);
            if (jsRuntime is null)
            {
                return;
            }

            var gate = new InteropGate(jsRuntime, start.Compilation.GetTypeByMetadataName(JsObjectReferenceMetadataName));
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, gate), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1601 when an interop call on a JavaScript-runtime receiver sits directly inside a loop.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="gate">The resolved JavaScript-interop types gating the rule.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, InteropGate gate)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        if (name is not (InvokeAsyncMethodName or InvokeVoidAsyncMethodName))
        {
            return;
        }

        if (!IsDirectlyInsideLoop(invocation))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (!IsJsInteropReceiver(receiverType, gate))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            BlazorRules.JsInteropInLoop,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Returns whether a node sits directly in a <c>for</c>/<c>foreach</c> body without crossing a function.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns>
    /// <see langword="true"/> when the nearest enclosing statement is a loop; <see langword="false"/> when a nested
    /// function is crossed first (its body owns the per-iteration cost) or no loop encloses the node.
    /// </returns>
    private static bool IsDirectlyInsideLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    return false;

                case ForStatementSyntax:
                case CommonForEachStatementSyntax:
                    return true;

                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Returns whether a receiver's type is, or implements, the JavaScript-runtime or object-reference interface.</summary>
    /// <param name="receiverType">The static type of the call's receiver.</param>
    /// <param name="gate">The resolved JavaScript-interop types.</param>
    /// <returns><see langword="true"/> when the receiver is a JavaScript-interop endpoint.</returns>
    private static bool IsJsInteropReceiver(ITypeSymbol? receiverType, InteropGate gate)
    {
        if (receiverType is null)
        {
            return false;
        }

        return ImplementsOrIs(receiverType, gate.JsRuntime)
            || (gate.JsObjectReference is not null && ImplementsOrIs(receiverType, gate.JsObjectReference));
    }

    /// <summary>Returns whether a type is the interface itself or implements it.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="interfaceType">The interface to match.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is or implements <paramref name="interfaceType"/>.</returns>
    private static bool ImplementsOrIs(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The JavaScript-interop types resolved once per compilation.</summary>
    /// <param name="JsRuntime">The runtime interface; always present while the rule is registered.</param>
    /// <param name="JsObjectReference">The object-reference interface, when the framework exposes one.</param>
    private readonly record struct InteropGate(INamedTypeSymbol JsRuntime, INamedTypeSymbol? JsObjectReference);
}
