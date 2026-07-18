// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an event whose delegate is not one of the framework's handler delegates (SST2304).
/// </summary>
/// <remarks>
/// <para>
/// Code that forwards one event to another, weakly subscribes to one, or binds one at runtime is written
/// once against <c>void (object sender, TEventArgs e)</c> published through <c>EventHandler</c> or
/// <c>EventHandler&lt;TEventArgs&gt;</c>. A bespoke delegate of any other shape works right up until
/// something generic has to handle it, and then it cannot be handled at all; a bespoke delegate that
/// already matches the shape still blocks handler reuse and adds API surface the framework delegate
/// provides for free.
/// </para>
/// <para>
/// The message names the tightest replacement the delegate's shape allows: the exact
/// <c>EventHandler&lt;TEventArgs&gt;</c> construction (or <c>EventHandler</c> when the payload is
/// <c>EventArgs</c> itself) when the invoke signature matches the shape, and the placeholder form when the
/// shape differs and an arguments type still has to be designed.
/// </para>
/// <para>
/// Skipped: an event that <b>overrides</b> one, or that <b>implements an interface member</b>. Its type is
/// dictated by the declaration it follows, so the fix belongs there, and that declaration is the one
/// reported. Generated code is skipped as always.
/// </para>
/// <para>
/// The rule suggests an API, so it proves that API is there first: a compilation with no
/// <c>System.EventHandler&lt;T&gt;</c> to move to is never told to move to it. The lookup — and the shape
/// walk that picks the message — sit behind every other test, so only an event that reports pays for them.
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

    /// <summary>The replacement named when the delegate's shape differs and the arguments type is still to be designed.</summary>
    private const string PlaceholderReplacement = "EventHandler<TEventArgs>";

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

    /// <summary>Reports an event whose delegate is bespoke where a framework handler would do.</summary>
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

        if (@event.Locations.Length == 0 || !@event.Locations[0].IsInSource)
        {
            return;
        }

        // The delegate the rule asks for is only asked for once the compilation is known to have it. The
        // lookup sits behind every other test, so it runs only for an event that would otherwise report.
        if (context.Compilation.GetTypeByMetadataName(EventHandlerMetadataName) is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DesignRules.EventHandlerSignature,
            @event.Locations[0],
            @event.Name,
            BuildReplacement(handler),
            handler.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    /// <summary>Names the tightest framework handler the delegate's shape allows.</summary>
    /// <param name="handler">The event's delegate type.</param>
    /// <returns>The concrete handler construction for a matching shape, the placeholder form otherwise.</returns>
    /// <remarks>Runs only after the violation is settled, so the display strings cost the clean path nothing.</remarks>
    private static string BuildReplacement(INamedTypeSymbol handler)
    {
        if (handler.DelegateInvokeMethod is not { } invoke || !HasStandardShape(invoke))
        {
            return PlaceholderReplacement;
        }

        var payload = invoke.Parameters[1].Type;
        if (payload is INamedTypeSymbol named && IsSystemEventArgs(named))
        {
            return EventHandlerName;
        }

        return "EventHandler<" + payload.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + ">";
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

    /// <summary>Returns whether a delegate's invoke method has the shape every event consumer assumes.</summary>
    /// <param name="invoke">The delegate's invoke method.</param>
    /// <returns><see langword="true"/> for <c>void (object sender, TEventArgs e)</c>.</returns>
    /// <remarks>A by-reference parameter is not the shape: it cannot bind to the framework handler.</remarks>
    private static bool HasStandardShape(IMethodSymbol invoke)
        => invoke.ReturnsVoid
            && invoke.Parameters.Length == HandlerParameterCount
            && invoke.Parameters[0] is { RefKind: RefKind.None, Type.SpecialType: SpecialType.System_Object }
            && invoke.Parameters[1] is { RefKind: RefKind.None } payload
            && DerivesFromEventArgs(payload.Type);

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
            if (IsSystemEventArgs(current))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is <c>System.EventArgs</c> itself.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for the framework's event payload base.</returns>
    private static bool IsSystemEventArgs(ITypeSymbol type)
        => string.Equals(type.Name, EventArgsName, StringComparison.Ordinal)
            && type.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true };
}
