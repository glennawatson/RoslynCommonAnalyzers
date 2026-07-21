// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>StateHasChanged()</c> called directly from a <c>System.Threading.Timer</c> or
/// <c>System.Timers.Timer</c> callback in a component, without marshalling through
/// <c>InvokeAsync(...)</c> (SST2710). The callback runs on a thread-pool thread rather than the renderer's
/// dispatcher, so requesting a render from there throws at runtime; the fix is
/// <c>InvokeAsync(StateHasChanged)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The rule works forward from the timer wiring: the callback delegate of a <c>new Timer(callback, …)</c>
/// (<c>System.Threading.Timer</c>), and the handler on a <c>timer.Elapsed += handler</c>
/// (<c>System.Timers.Timer</c>). A lambda or anonymous-method callback is scanned in place; a method-group
/// callback is followed to its declaration when that declaration is in the same file. Inside the callback
/// body, every <c>StateHasChanged()</c> call that is not lexically an argument of an <c>InvokeAsync(...)</c>
/// call is reported — the method-group form <c>InvokeAsync(StateHasChanged)</c> is already safe and is
/// never flagged.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on <c>ComponentBase</c> resolving and on at least one timer
/// type being present, so a non-component or non-timer project registers nothing. Explicit
/// <c>new Timer(…)</c> creations are filtered by the written type name before any binding.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2710TimerStateHasChangedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the thread-pool timer whose callback runs off the dispatcher.</summary>
    private const string ThreadingTimerMetadataName = "System.Threading.Timer";

    /// <summary>The metadata name of the component-model timer whose <c>Elapsed</c> handler runs off the dispatcher.</summary>
    private const string TimersTimerMetadataName = "System.Timers.Timer";

    /// <summary>The simple name shared by both timer types.</summary>
    private const string TimerTypeName = "Timer";

    /// <summary>The event a <c>System.Timers.Timer</c> raises on each tick.</summary>
    private const string ElapsedEventName = "Elapsed";

    /// <summary>The dispatcher-marshalling method that makes a render request from a callback safe.</summary>
    private const string InvokeAsyncName = "InvokeAsync";

    /// <summary>The cached visitor that flags each unmarshalled render request in a callback body.</summary>
    private static readonly DescendantTraversalHelper.DescendantVisitor<InvocationExpressionSyntax, RenderRequestScan> RenderRequestVisitor = VisitInvocation;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.TimerStateHasChangedOffDispatcher);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (BlazorComponentModel.Create(start.Compilation) is not { } model)
            {
                return;
            }

            var compilation = start.Compilation;
            var threadingTimer = compilation.GetTypeByMetadataName(ThreadingTimerMetadataName);
            var timersTimer = compilation.GetTypeByMetadataName(TimersTimerMetadataName);

            if (threadingTimer is not null)
            {
                start.RegisterSyntaxNodeAction(
                    nodeContext => AnalyzeThreadingTimerCreation(nodeContext, model, threadingTimer),
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression);
            }

            if (timersTimer is not null)
            {
                start.RegisterSyntaxNodeAction(
                    nodeContext => AnalyzeTimersElapsedSubscription(nodeContext, model, timersTimer),
                    SyntaxKind.AddAssignmentExpression);
            }
        });
    }

    /// <summary>Analyzes a <c>new System.Threading.Timer(callback, …)</c> construction.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="model">The component model resolved for this compilation.</param>
    /// <param name="threadingTimer">The resolved <c>System.Threading.Timer</c> type.</param>
    private static void AnalyzeThreadingTimerCreation(SyntaxNodeAnalysisContext context, BlazorComponentModel model, INamedTypeSymbol threadingTimer)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: > 0 } argumentList
            || (creation is ObjectCreationExpressionSyntax explicitCreation && !IsTimerNamed(explicitCreation.Type)))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, threadingTimer))
        {
            return;
        }

        AnalyzeCallback(context, model, argumentList.Arguments[0].Expression);
    }

    /// <summary>Analyzes a <c>timer.Elapsed += handler</c> subscription on a <c>System.Timers.Timer</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="model">The component model resolved for this compilation.</param>
    /// <param name="timersTimer">The resolved <c>System.Timers.Timer</c> type.</param>
    private static void AnalyzeTimersElapsedSubscription(SyntaxNodeAnalysisContext context, BlazorComponentModel model, INamedTypeSymbol timersTimer)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (assignment.Left is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: ElapsedEventName } memberAccess
            || context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol is not IEventSymbol eventSymbol
            || !SymbolEqualityComparer.Default.Equals(eventSymbol.ContainingType, timersTimer))
        {
            return;
        }

        AnalyzeCallback(context, model, assignment.Right);
    }

    /// <summary>Scans a timer callback body for a render request that is not marshalled onto the dispatcher.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="model">The component model resolved for this compilation.</param>
    /// <param name="callback">The callback delegate expression.</param>
    private static void AnalyzeCallback(SyntaxNodeAnalysisContext context, BlazorComponentModel model, ExpressionSyntax callback)
    {
        if (GetCallbackScanRoot(context, callback) is not { } root)
        {
            return;
        }

        var scan = new RenderRequestScan(context, model, root);
        DescendantTraversalHelper.VisitDescendants(root, ref scan, RenderRequestVisitor);
    }

    /// <summary>Flags one invocation when it is a render request the callback does not marshal onto the dispatcher.</summary>
    /// <param name="node">The current invocation.</param>
    /// <param name="state">The render-request scan state.</param>
    /// <returns>Always <see langword="true"/>: every render request in the callback is flagged.</returns>
    private static bool VisitInvocation(InvocationExpressionSyntax node, ref RenderRequestScan state)
    {
        if (!BlazorComponentModel.IsSelfStateHasChangedSyntax(node.Expression)
            || IsInvokeAsyncArgument(node, state.Root)
            || !state.Components.IsStateHasChanged(state.Context.SemanticModel.GetSymbolInfo(node, state.Context.CancellationToken).Symbol as IMethodSymbol))
        {
            return true;
        }

        state.Context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.TimerStateHasChangedOffDispatcher, node.GetLocation()));
        return true;
    }

    /// <summary>Returns the node whose descendants make up a callback: the delegate itself, or a same-file method-group target.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="callback">The callback delegate expression.</param>
    /// <returns>The scan root, or <see langword="null"/> when it cannot be reached from here.</returns>
    private static SyntaxNode? GetCallbackScanRoot(SyntaxNodeAnalysisContext context, ExpressionSyntax callback) => callback switch
    {
        SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax or AnonymousMethodExpressionSyntax => callback,
        IdentifierNameSyntax or MemberAccessExpressionSyntax => GetMethodGroupDeclaration(context, callback),
        _ => null,
    };

    /// <summary>Returns the declaration of the method a method-group callback names, when it is in the same file.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="callback">The method-group callback expression.</param>
    /// <returns>The target method's declaration, or <see langword="null"/> when it is unresolved or declared elsewhere.</returns>
    private static MethodDeclarationSyntax? GetMethodGroupDeclaration(SyntaxNodeAnalysisContext context, ExpressionSyntax callback)
    {
        if (context.SemanticModel.GetSymbolInfo(callback, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.DeclaringSyntaxReferences is not [var reference]
            || reference.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax declaration
            || declaration.SyntaxTree != context.Node.SyntaxTree)
        {
            return null;
        }

        return declaration;
    }

    /// <summary>Returns whether a written type name's rightmost segment is <c>Timer</c>.</summary>
    /// <param name="type">The created type's syntax.</param>
    /// <returns><see langword="true"/> for <c>Timer</c> or a qualified name ending in <c>Timer</c>.</returns>
    private static bool IsTimerNamed(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax { Identifier.ValueText: TimerTypeName } => true,
        QualifiedNameSyntax { Right.Identifier.ValueText: TimerTypeName } => true,
        _ => false,
    };

    /// <summary>Returns whether a node sits inside an <c>InvokeAsync(...)</c> call within the callback body.</summary>
    /// <param name="node">The render-request invocation.</param>
    /// <param name="root">The callback body bounding the walk.</param>
    /// <returns><see langword="true"/> when an enclosing <c>InvokeAsync</c> call already marshals it onto the dispatcher.</returns>
    private static bool IsInvokeAsyncArgument(SyntaxNode node, SyntaxNode root)
    {
        for (var current = node.Parent; current is not null && current != root.Parent; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation
                && string.Equals(GetInvokedName(invocation.Expression), InvokeAsyncName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the invoked member's simple name for an <c>Identifier(...)</c> or <c>x.Identifier(...)</c> call.</summary>
    /// <param name="expression">The invocation's callee expression.</param>
    /// <returns>The simple name, or <see langword="null"/> when the callee is not a plain member reference.</returns>
    private static string? GetInvokedName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The state threaded through a timer callback body's render-request walk.</summary>
    /// <param name="Context">The syntax node analysis context.</param>
    /// <param name="Components">The component model resolved for this compilation.</param>
    /// <param name="Root">The callback body bounding the wrap-detection walk.</param>
    private readonly record struct RenderRequestScan(SyntaxNodeAnalysisContext Context, BlazorComponentModel Components, SyntaxNode Root);
}
