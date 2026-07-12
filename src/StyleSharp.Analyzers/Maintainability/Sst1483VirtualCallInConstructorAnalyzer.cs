// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a constructor that calls a member a derived type can still override (SST1483).
/// </summary>
/// <remarks>
/// <para>
/// Construction runs base-first: the base constructor finishes before a single derived field initializer has
/// run. A virtual call made from it dispatches to the derived override anyway, so the override runs against an
/// object that does not exist yet — its fields are still at their defaults and the <c>readonly</c> ones it
/// reads are <see langword="null"/>. The bug is invisible in the base type and only appears once somebody
/// derives.
/// </para>
/// <para>
/// "Overridable" is wider than <c>virtual</c>. An <c>abstract</c> member is overridable, and so is an
/// <c>override</c> — an override stays open until it is <c>sealed</c>, so a type further down can replace it
/// again. What is not overridable: anything in a <c>sealed</c> type or a struct, a <c>sealed override</c>, and
/// a <c>base.</c> call, which the compiler emits as a non-virtual call that always lands in the base
/// implementation and can therefore never reach a derived override.
/// </para>
/// <para>
/// Only the constructor's own body is walked. A lambda or a local function declared there runs when it is
/// invoked, which may be long after the constructor has returned, so neither is followed. The other places
/// construction runs code — a constructor initializer, a field or property initializer, a primary
/// constructor's base arguments — cannot reach an instance member at all (CS0027, CS0120, CS0236), so there is
/// no virtual call for them to make and nothing there to analyze.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1483VirtualCallInConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The contextual keyword whose operand is a name, not a call.</summary>
    private const string NameOfKeyword = "nameof";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.VirtualCallInConstructor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConstructorDeclaration);
    }

    /// <summary>Walks a constructor body for calls that a derived type could still take over.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        if (IsClosedToOverrides(constructor))
        {
            return;
        }

        if (GetBody(constructor) is not { } body)
        {
            return;
        }

        // The syntactic check above reads one declaration; 'sealed' may sit on a different partial half of the
        // same type. The driver has already handed us the constructor's symbol, so confirming costs no bind.
        if (context.ContainingSymbol?.ContainingType is not { IsSealed: false } containingType)
        {
            return;
        }

        Walk(body, containingType, in context);
    }

    /// <summary>Returns whether nothing declared on this constructor's type can be overridden.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <returns><see langword="true"/> when a derived override is impossible.</returns>
    /// <remarks>
    /// A static constructor has no <c>this</c> to dispatch on. A struct — plain or a record struct — is
    /// implicitly sealed. A <c>sealed</c> class or record has no derived type to be surprised by. All three are
    /// read off the modifier tokens, which is why the common case costs nothing.
    /// </remarks>
    private static bool IsClosedToOverrides(ConstructorDeclarationSyntax constructor)
    {
        if (ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword))
        {
            return true;
        }

        return constructor.Parent switch
        {
            RecordDeclarationSyntax record => record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                || ModifierListHelper.Contains(record.Modifiers, SyntaxKind.SealedKeyword),
            ClassDeclarationSyntax type => ModifierListHelper.Contains(type.Modifiers, SyntaxKind.SealedKeyword),
            _ => true,
        };
    }

    /// <summary>Gets the code a constructor runs, whichever body form it uses.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <returns>The body, or <see langword="null"/> when the constructor declares none.</returns>
    /// <remarks>
    /// The <c>: base(...)</c> / <c>: this(...)</c> initializer is deliberately not walked. Its arguments are
    /// evaluated before <c>this</c> exists, so the compiler rejects any instance member there (CS0120) and it
    /// cannot contain a virtual call to find.
    /// </remarks>
    private static SyntaxNode? GetBody(ConstructorDeclarationSyntax constructor)
        => (SyntaxNode?)constructor.Body ?? constructor.ExpressionBody?.Expression;

    /// <summary>Visits a constructor body, binding only the names that could dispatch on <c>this</c>.</summary>
    /// <param name="node">The node to visit.</param>
    /// <param name="containingType">The constructor's type.</param>
    /// <param name="context">The syntax node context.</param>
    private static void Walk(SyntaxNode node, INamedTypeSymbol containingType, in SyntaxNodeAnalysisContext context)
    {
        if (DispatchesNothing(node))
        {
            return;
        }

        switch (node)
        {
            // Only 'this.X' dispatches virtually on the object under construction. 'base.X' is emitted as a
            // non-virtual call — it is the fix, not the bug — and 'other.X' is somebody else's object, so in
            // both cases only the receiver is followed and the member half is dropped.
            case MemberAccessExpressionSyntax memberAccess:
            {
                WalkMemberAccess(memberAccess, containingType, in context);
                return;
            }

            // 'Member = value' in an initializer names a member of the object being built, not of this one,
            // so only the values are followed.
            case InitializerExpressionSyntax initializer when IsMemberInitializer(initializer):
            {
                WalkInitializerValues(initializer, containingType, in context);
                return;
            }

            // A property pattern's name belongs to the type being matched, and an argument or member name
            // belongs to the callee.
            case SubpatternSyntax subpattern:
            {
                Walk(subpattern.Pattern, containingType, in context);
                return;
            }

            case SimpleNameSyntax name:
            {
                Inspect(name, containingType, in context);
                return;
            }
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is { } child)
            {
                Walk(child, containingType, in context);
            }
        }
    }

    /// <summary>Returns whether a node is one the walk steps over without binding anything under it.</summary>
    /// <param name="node">The node being visited.</param>
    /// <returns><see langword="true"/> when nothing in the node can dispatch on the object under construction.</returns>
    /// <remarks>
    /// A lambda or a local function is a value, not a call: it runs when something invokes it, which may be
    /// long after construction, so following it would report code that never runs on a half-built object.
    /// <c>nameof(Render)</c> yields a name at compile time and dispatches nothing. The rest are names that never
    /// denote a call on this instance: the member half of a <c>?.</c>, whose receiver the conditional access has
    /// already offered up, and an argument name, a member name or a qualified type name, each of which belongs
    /// to the callee or to the type rather than to <c>this</c>.
    /// </remarks>
    private static bool DispatchesNothing(SyntaxNode node) => node is AnonymousFunctionExpressionSyntax
        or LocalFunctionStatementSyntax
        or MemberBindingExpressionSyntax
        or NameColonSyntax
        or NameEqualsSyntax
        or QualifiedNameSyntax
        or AliasQualifiedNameSyntax
        or InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: NameOfKeyword } };

    /// <summary>Follows a member access, binding its member half only when it dispatches on <c>this</c>.</summary>
    /// <param name="memberAccess">The member access.</param>
    /// <param name="containingType">The constructor's type.</param>
    /// <param name="context">The syntax node context.</param>
    private static void WalkMemberAccess(
        MemberAccessExpressionSyntax memberAccess,
        INamedTypeSymbol containingType,
        in SyntaxNodeAnalysisContext context)
    {
        if (memberAccess.Expression is ThisExpressionSyntax)
        {
            Inspect(memberAccess.Name, containingType, in context);
            return;
        }

        Walk(memberAccess.Expression, containingType, in context);
    }

    /// <summary>Follows only the assigned values of an object or <c>with</c> initializer.</summary>
    /// <param name="initializer">The initializer.</param>
    /// <param name="containingType">The constructor's type.</param>
    /// <param name="context">The syntax node context.</param>
    private static void WalkInitializerValues(
        InitializerExpressionSyntax initializer,
        INamedTypeSymbol containingType,
        in SyntaxNodeAnalysisContext context)
    {
        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            var expression = expressions[i];
            Walk(expression is AssignmentExpressionSyntax assignment ? assignment.Right : expression, containingType, in context);
        }
    }

    /// <summary>Reports a name that dispatches to a member a derived type could override.</summary>
    /// <param name="name">The name to bind.</param>
    /// <param name="containingType">The constructor's type.</param>
    /// <param name="context">The syntax node context.</param>
    private static void Inspect(SimpleNameSyntax name, INamedTypeSymbol containingType, in SyntaxNodeAnalysisContext context)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
        if (symbol is null || !IsOverridable(symbol) || !RunsDuringConstruction(symbol, name, containingType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.VirtualCallInConstructor,
            name.GetLocation(),
            symbol.Name));
    }

    /// <summary>Returns whether a derived type can still replace a member's implementation.</summary>
    /// <param name="symbol">The bound member.</param>
    /// <returns><see langword="true"/> when the member is open to an override.</returns>
    /// <remarks>
    /// An <c>override</c> is included: overriding a member does not close it, so a type further down the chain
    /// can override it again. <c>sealed</c> is what closes it. A <c>private</c> or non-virtual member is never
    /// any of these, so it never reaches the report.
    /// </remarks>
    private static bool IsOverridable(ISymbol symbol)
        => !symbol.IsStatic
            && !symbol.IsSealed
            && (symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride);

    /// <summary>Returns whether reaching a member's name actually runs its code during construction.</summary>
    /// <param name="symbol">The bound member.</param>
    /// <param name="name">The name that referenced it.</param>
    /// <param name="containingType">The constructor's type.</param>
    /// <returns><see langword="true"/> when the reference dispatches to an override.</returns>
    /// <remarks>
    /// Naming a method is not calling one: <c>_handler = Render;</c> builds a delegate that dispatches when
    /// something later invokes it, which is not during construction. A property is the opposite — every read
    /// and every write runs an accessor, so naming it at all is the call.
    /// </remarks>
    private static bool RunsDuringConstruction(ISymbol symbol, SimpleNameSyntax name, INamedTypeSymbol containingType)
        => symbol switch
        {
            IMethodSymbol => IsInvoked(name),
            IPropertySymbol => true,
            IEventSymbol @event => IsSubscription(name) && !IsBackingFieldAccess(@event, containingType),
            _ => false,
        };

    /// <summary>Returns whether a name is the target of a call rather than a method group.</summary>
    /// <param name="name">The name that referenced the member.</param>
    /// <returns><see langword="true"/> when the member is invoked here.</returns>
    private static bool IsInvoked(SimpleNameSyntax name)
        => GetReferencingExpression(name) is { Parent: InvocationExpressionSyntax invocation } expression
            && ReferenceEquals(invocation.Expression, expression);

    /// <summary>Returns whether a name is the target of an event subscription.</summary>
    /// <param name="name">The name that referenced the member.</param>
    /// <returns><see langword="true"/> when a handler is added to or removed from the event here.</returns>
    private static bool IsSubscription(SimpleNameSyntax name)
        => GetReferencingExpression(name) is { Parent: AssignmentExpressionSyntax assignment } expression
            && ReferenceEquals(assignment.Left, expression)
            && (assignment.IsKind(SyntaxKind.AddAssignmentExpression) || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression));

    /// <summary>Gets the expression a member name forms, which is the access when the name is qualified.</summary>
    /// <param name="name">The name that referenced the member.</param>
    /// <returns>The enclosing member access, or the name itself.</returns>
    private static ExpressionSyntax GetReferencingExpression(SimpleNameSyntax name)
        => name.Parent is MemberAccessExpressionSyntax memberAccess && ReferenceEquals(memberAccess.Name, name)
            ? memberAccess
            : name;

    /// <summary>Returns whether an event reference reads the backing field instead of calling an accessor.</summary>
    /// <param name="event">The bound event.</param>
    /// <param name="containingType">The constructor's type.</param>
    /// <returns><see langword="true"/> when no accessor runs, so nothing dispatches.</returns>
    /// <remarks>
    /// Inside the type that declares it, a field-like event <em>is</em> its backing field: <c>Changed += h</c>
    /// combines delegates in the field and never calls <c>add_Changed</c>, so a derived override cannot see it.
    /// An abstract event has no field, and an event with hand-written accessors is not field-like, so both of
    /// those really do dispatch — as does any event inherited from a base type.
    /// </remarks>
    private static bool IsBackingFieldAccess(IEventSymbol @event, INamedTypeSymbol containingType)
        => !@event.IsAbstract
            && @event.AddMethod is { IsImplicitlyDeclared: true }
            && SymbolEqualityComparer.Default.Equals(@event.ContainingType, containingType);

    /// <summary>Returns whether an initializer assigns members of the object being built.</summary>
    /// <param name="initializer">The initializer.</param>
    /// <returns><see langword="true"/> for an object initializer or a <c>with</c> initializer.</returns>
    private static bool IsMemberInitializer(InitializerExpressionSyntax initializer)
        => initializer.IsKind(SyntaxKind.ObjectInitializerExpression)
            || initializer.IsKind(SyntaxKind.WithInitializerExpression);
}
