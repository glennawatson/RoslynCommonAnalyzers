// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an event whose delegate does not have the shape every consumer of events assumes (SST2304).
/// </summary>
/// <remarks>
/// <para>
/// The shape is <c>void (object sender, TEventArgs e)</c> where <c>TEventArgs</c> derives from
/// <c>EventArgs</c>. Code that forwards one event to another, weakly subscribes to one, or binds one at
/// runtime is written once against that shape; a delegate of any other shape works right up until
/// something generic has to handle it, and then it cannot be handled at all.
/// </para>
/// <para>
/// What is reported is the delegate's shape, not its name: a hand-written
/// <c>delegate void Changed(object sender, ValueChangedEventArgs e)</c> is exactly the right shape and is
/// left alone — the rule is not a campaign for <c>EventHandler&lt;T&gt;</c>, it is a campaign against
/// <c>delegate void Changed(int oldValue, int newValue)</c>. <c>EventHandler</c> and
/// <c>EventHandler&lt;T&gt;</c> are the shape by definition and are never examined.
/// </para>
/// <para>
/// Skipped: an event that <b>overrides</b> one, or that <b>implements an interface member</b>. Its type is
/// dictated by the declaration it follows, so the fix belongs there, and that declaration is the one
/// reported. Generated code is skipped as always.
/// </para>
/// <para>
/// The rule suggests an API, so it proves that API is there first: a compilation with no
/// <c>System.EventHandler&lt;T&gt;</c> to move to is never told to move to it. The lookup sits behind every
/// other test, so only an event that would otherwise be reported pays for it.
/// </para>
/// <para>
/// Events are rare enough that a symbol action on them costs nothing on a compilation that declares none,
/// and the common case — an <c>EventHandler&lt;T&gt;</c> — is settled by two string comparisons before
/// anything else is looked at.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2304EventHandlerSignatureAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The unqualified name of both framework handler delegates.</summary>
    private const string EventHandlerName = "EventHandler";

    /// <summary>The unqualified name of the base every event payload derives from.</summary>
    private const string EventArgsName = "EventArgs";

    /// <summary>The metadata name of the generic handler delegate the rule suggests.</summary>
    private const string EventHandlerMetadataName = "System.EventHandler`1";

    /// <summary>The number of parameters the standard handler shape declares.</summary>
    private const int HandlerParameterCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.EventHandlerSignature);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeEvent, SymbolKind.Event);
    }

    /// <summary>Reports an event whose delegate cannot be handled by code that has not met it.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeEvent(SymbolAnalysisContext context)
    {
        var @event = (IEventSymbol)context.Symbol;
        if (@event.IsOverride || @event.ExplicitInterfaceImplementations.Length > 0 || ImplementsInterfaceEvent(@event))
        {
            return;
        }

        if (@event.Type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } handler || IsFrameworkHandler(handler))
        {
            return;
        }

        if (HasStandardShape(handler) || @event.Locations.Length == 0 || !@event.Locations[0].IsInSource)
        {
            return;
        }

        // The shape the rule asks for is only asked for once the compilation is known to have it. The
        // lookup sits behind every other test, so it runs only for an event that would otherwise report.
        if (context.Compilation.GetTypeByMetadataName(EventHandlerMetadataName) is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DesignRules.EventHandlerSignature, @event.Locations[0], @event.Name));
    }

    /// <summary>Returns whether an event implicitly implements an interface's event.</summary>
    /// <param name="event">The event to test.</param>
    /// <returns><see langword="true"/> when an interface dictates the event's type.</returns>
    /// <remarks>The interface walk runs only for an event, which is rare, and stops at the first match.</remarks>
    private static bool ImplementsInterfaceEvent(IEventSymbol @event)
    {
        if (@event.ContainingType is not { } containingType)
        {
            return false;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(@event.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidates[j]), @event))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a delegate is one of the framework's handler delegates.</summary>
    /// <param name="handler">The event's delegate type.</param>
    /// <returns><see langword="true"/> for <c>EventHandler</c> and <c>EventHandler&lt;T&gt;</c>.</returns>
    private static bool IsFrameworkHandler(INamedTypeSymbol handler)
        => string.Equals(handler.Name, EventHandlerName, StringComparison.Ordinal)
            && handler.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true };

    /// <summary>Returns whether a delegate has the shape every event consumer assumes.</summary>
    /// <param name="handler">The event's delegate type.</param>
    /// <returns><see langword="true"/> for <c>void (object sender, TEventArgs e)</c>.</returns>
    private static bool HasStandardShape(INamedTypeSymbol handler)
    {
        if (handler.DelegateInvokeMethod is not { } invoke
            || !invoke.ReturnsVoid
            || invoke.Parameters.Length != HandlerParameterCount)
        {
            return false;
        }

        return invoke.Parameters[0].Type.SpecialType == SpecialType.System_Object
            && DerivesFromEventArgs(invoke.Parameters[1].Type);
    }

    /// <summary>Returns whether a type is <c>EventArgs</c> or derives from it.</summary>
    /// <param name="type">The delegate's second parameter type.</param>
    /// <returns><see langword="true"/> when the parameter carries an event payload.</returns>
    /// <remarks>
    /// The base chain is walked by name rather than resolved through a well-known-type lookup, so a
    /// compilation that declares no events never resolves anything. A type parameter constrained to
    /// <c>EventArgs</c> counts through its constraint: a generic delegate is as usable as the constraint
    /// it promises.
    /// </remarks>
    private static bool DerivesFromEventArgs(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            var constraints = typeParameter.ConstraintTypes;
            for (var i = 0; i < constraints.Length; i++)
            {
                if (DerivesFromEventArgs(constraints[i]))
                {
                    return true;
                }
            }

            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.Name, EventArgsName, StringComparison.Ordinal)
                && current.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true })
            {
                return true;
            }
        }

        return false;
    }
}
