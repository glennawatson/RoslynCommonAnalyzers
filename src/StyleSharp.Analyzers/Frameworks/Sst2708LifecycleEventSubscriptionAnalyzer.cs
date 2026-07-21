// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a component that subscribes to an external .NET event inside a render-lifecycle method
/// (<c>OnInitialized</c>, <c>OnParametersSet</c>, <c>OnAfterRender</c> and their async forms) with
/// <c>source.Event += handler</c> but never removes that subscription (SST2708). The delegate roots the
/// component in the longer-lived event source, so on a server-rendered circuit every render or navigation
/// leaks one component instance for the life of the source.
/// </summary>
/// <remarks>
/// <para>
/// This is the complement to the disposable-field rules (SST2315, SST2316): they watch a resource a type
/// creates and never disposes; this watches an event handoff those rules never see. Only a
/// <c>source.Event += handler</c> whose receiver is not <c>this</c>/<c>base</c> is considered — an
/// own-event subscription roots nothing external — and the left side must bind to an event, so a plain
/// delegate-field <c>+=</c> is ignored. A subscription is treated as handled when a matching
/// <c>source.Event -= handler</c> for the same event appears anywhere in the component, which is where a
/// <c>Dispose</c>/<c>DisposeAsync</c> unsubscribe clears it; the walk for <c>-=</c> runs only once a
/// lifecycle subscription has been found.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on <c>ComponentBase</c> resolving, so a non-component
/// project registers nothing. Every class with a base list is bound to check it derives from
/// <c>ComponentBase</c>; only a component then has its lifecycle-method bodies scanned.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2708LifecycleEventSubscriptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The cached visitor that collects external event subscriptions in a lifecycle body.</summary>
    private static readonly DescendantTraversalHelper.DescendantVisitor<AssignmentExpressionSyntax, SubscriptionScan> SubscriptionVisitor = VisitSubscription;

    /// <summary>The cached visitor that records every event unsubscribed anywhere in the component.</summary>
    private static readonly DescendantTraversalHelper.DescendantVisitor<AssignmentExpressionSyntax, UnsubscribeScan> UnsubscribeVisitor = VisitUnsubscribe;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.LifecycleEventSubscriptionLeak);

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

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, model), SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>Analyzes one class for lifecycle event subscriptions that are never removed.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="model">The component model resolved for this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, BlazorComponentModel model)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        if (classDeclaration.BaseList is null
            || context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken) is not { } type
            || !model.DerivesFromComponentBase(type))
        {
            return;
        }

        if (CollectLifecycleSubscriptions(context, classDeclaration) is not { } subscriptions)
        {
            return;
        }

        ReportUnremovedSubscriptions(context, classDeclaration, subscriptions);
    }

    /// <summary>Collects the external event subscriptions made inside the component's lifecycle methods.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="classDeclaration">The component declaration.</param>
    /// <returns>The subscriptions found, or <see langword="null"/> when there are none.</returns>
    private static List<EventSubscription>? CollectLifecycleSubscriptions(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
    {
        var scan = new SubscriptionScan(context.SemanticModel, context.CancellationToken);
        var members = classDeclaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is MethodDeclarationSyntax method
                && method.Modifiers.Any(SyntaxKind.OverrideKeyword)
                && BlazorComponentModel.IsLifecycleMethodName(method.Identifier.ValueText)
                && ((SyntaxNode?)method.Body ?? method.ExpressionBody) is { } body)
            {
                DescendantTraversalHelper.VisitDescendants(body, ref scan, SubscriptionVisitor);
            }
        }

        return scan.Subscriptions;
    }

    /// <summary>Reports each collected subscription the component never removes.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="classDeclaration">The component declaration.</param>
    /// <param name="subscriptions">The subscriptions found in lifecycle methods.</param>
    private static void ReportUnremovedSubscriptions(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration, List<EventSubscription> subscriptions)
    {
        for (var i = 0; i < subscriptions.Count; i++)
        {
            var subscription = subscriptions[i];
            var scan = new UnsubscribeScan(context.SemanticModel, subscription.EventSymbol, context.CancellationToken);
            DescendantTraversalHelper.VisitDescendants(classDeclaration, ref scan, UnsubscribeVisitor);
            if (!scan.Found)
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.LifecycleEventSubscriptionLeak, subscription.Location, subscription.EventName));
            }
        }
    }

    /// <summary>Records an external <c>source.Event += handler</c> subscription for later matching.</summary>
    /// <param name="node">The current assignment.</param>
    /// <param name="state">The subscription scan state.</param>
    /// <returns>Always <see langword="true"/>: every subscription in the body is collected.</returns>
    private static bool VisitSubscription(AssignmentExpressionSyntax node, ref SubscriptionScan state)
    {
        if (!node.IsKind(SyntaxKind.AddAssignmentExpression)
            || node.Left is not MemberAccessExpressionSyntax { Expression: not (ThisExpressionSyntax or BaseExpressionSyntax) } memberAccess
            || state.Model.GetSymbolInfo(memberAccess, state.CancellationToken).Symbol is not IEventSymbol eventSymbol)
        {
            return true;
        }

        state.Add(new EventSubscription(eventSymbol, memberAccess.GetLocation(), eventSymbol.Name));
        return true;
    }

    /// <summary>Marks the scan found when a <c>-=</c> removes the subscription's event.</summary>
    /// <param name="node">The current assignment.</param>
    /// <param name="state">The unsubscribe scan state.</param>
    /// <returns><see langword="false"/> once a matching <c>-=</c> is found, to stop the walk; otherwise <see langword="true"/>.</returns>
    private static bool VisitUnsubscribe(AssignmentExpressionSyntax node, ref UnsubscribeScan state)
    {
        if (!node.IsKind(SyntaxKind.SubtractAssignmentExpression)
            || node.Left is not MemberAccessExpressionSyntax memberAccess
            || state.Model.GetSymbolInfo(memberAccess, state.CancellationToken).Symbol is not IEventSymbol eventSymbol
            || !SymbolEqualityComparer.Default.Equals(eventSymbol, state.Target))
        {
            return true;
        }

        state.MarkFound();
        return false;
    }

    /// <summary>An external event subscription made inside a lifecycle method.</summary>
    /// <param name="EventSymbol">The subscribed event.</param>
    /// <param name="Location">The subscription's location, for the diagnostic.</param>
    /// <param name="EventName">The event's name, for the message.</param>
    private readonly record struct EventSubscription(IEventSymbol EventSymbol, Location Location, string EventName);

    /// <summary>The state threaded through the lifecycle-body subscription walk.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="CancellationToken">A token that cancels the walk.</param>
    private record struct SubscriptionScan(SemanticModel Model, CancellationToken CancellationToken)
    {
        /// <summary>Gets the subscriptions collected so far, or <see langword="null"/> when none have been seen.</summary>
        public List<EventSubscription>? Subscriptions { get; private set; }

        /// <summary>Records one external subscription.</summary>
        /// <param name="subscription">The subscription to record.</param>
        public void Add(EventSubscription subscription) => (Subscriptions ??= new List<EventSubscription>(4)).Add(subscription);
    }

    /// <summary>The state threaded through the component-wide search for a matching <c>-=</c>.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="Target">The subscribed event a matching <c>-=</c> must remove.</param>
    /// <param name="CancellationToken">A token that cancels the walk.</param>
    private record struct UnsubscribeScan(SemanticModel Model, IEventSymbol Target, CancellationToken CancellationToken)
    {
        /// <summary>Gets a value indicating whether a matching <c>-=</c> was found.</summary>
        public bool Found { get; private set; }

        /// <summary>Marks the target event as removed somewhere in the component.</summary>
        public void MarkFound() => Found = true;
    }
}
