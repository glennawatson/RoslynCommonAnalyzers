// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a type that declares a public <c>Dispose()</c> or <c>DisposeAsync()</c> but does not
/// implement the matching interface (SST2316). Cleanup that only exists as a method, with no
/// <c>IDisposable</c>/<c>IAsyncDisposable</c> to reach it, is never run by the owners that dispose
/// through the interface — DI containers, service scopes, composite disposers — so the resource is held
/// for the life of the process.
/// </summary>
/// <remarks>
/// <para>
/// The exemptions are the rule. A <c>ref struct</c> is never reported: its <c>using</c> binds to a
/// pattern <c>Dispose()</c> by duck-typing, and it may not have been allowed an interface at all. A
/// duck-typed enumerator, with <c>MoveNext()</c> and <c>Current</c>, is disposed by <c>foreach</c>
/// through the pattern. An explicit interface implementation, and a <c>Dispose()</c> inherited from a
/// base that already implements <c>IDisposable</c>, both leave the type implementing the interface, so
/// they never match.
/// </para>
/// <para>
/// The prepass is a name lookup: a type that declares no member named <c>Dispose</c> or
/// <c>DisposeAsync</c> — almost every type — exits before anything else runs. The async half is silent
/// where the framework has no <c>IAsyncDisposable</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2316DisposeWithoutInterfaceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key naming the interface a code fix should add.</summary>
    internal const string InterfaceKey = "Interface";

    /// <summary>The synchronous disposal method name.</summary>
    private const string DisposeName = "Dispose";

    /// <summary>The asynchronous disposal method name.</summary>
    private const string DisposeAsyncName = "DisposeAsync";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.DisposeWithoutInterface);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (DisposableTypes.Create(start.Compilation) is not { } types)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, types), SymbolKind.NamedType);
        });
    }

    /// <summary>Analyzes one named type for an orphaned disposal method.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    private static void Analyze(SymbolAnalysisContext context, in DisposableTypes types)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // A ref struct disposes by pattern, and only a class or struct can own the mistake this reports.
        if (type.IsRefLikeType || type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        var disposeMembers = type.GetMembers(DisposeName);
        var disposeAsyncMembers = type.GetMembers(DisposeAsyncName);
        if ((disposeMembers.Length == 0 && disposeAsyncMembers.Length == 0) || IsDuckTypedEnumerator(type))
        {
            return;
        }

        ReportOrphan(context, types, type, disposeMembers, disposeAsyncMembers);
    }

    /// <summary>Reports the orphaned disposal method, preferring the synchronous one.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    /// <param name="type">The type being analyzed.</param>
    /// <param name="disposeMembers">The members named <c>Dispose</c>.</param>
    /// <param name="disposeAsyncMembers">The members named <c>DisposeAsync</c>.</param>
    private static void ReportOrphan(
        SymbolAnalysisContext context,
        in DisposableTypes types,
        INamedTypeSymbol type,
        ImmutableArray<ISymbol> disposeMembers,
        ImmutableArray<ISymbol> disposeAsyncMembers)
    {
        if (FindSyncDisposeMethod(disposeMembers) is { } dispose && !types.ImplementsSyncDisposable(type))
        {
            Report(context, dispose, type, DisposeName, "IDisposable");
            return;
        }

        if (types.AsyncDisposable is null
            || FindAsyncDisposeMethod(disposeAsyncMembers) is not { } disposeAsync
            || types.ImplementsAsyncDisposable(type))
        {
            return;
        }

        Report(context, disposeAsync, type, DisposeAsyncName, "IAsyncDisposable");
    }

    /// <summary>Finds a public parameterless <c>void Dispose()</c> declared on the type.</summary>
    /// <param name="members">The members named <c>Dispose</c>.</param>
    /// <returns>The matching method, or <see langword="null"/>.</returns>
    private static IMethodSymbol? FindSyncDisposeMethod(ImmutableArray<ISymbol> members)
    {
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { Parameters.Length: 0, IsStatic: false, ReturnsVoid: true, DeclaredAccessibility: Accessibility.Public } method)
            {
                return method;
            }
        }

        return null;
    }

    /// <summary>Finds a public parameterless task-returning <c>DisposeAsync()</c> declared on the type.</summary>
    /// <param name="members">The members named <c>DisposeAsync</c>.</param>
    /// <returns>The matching method, or <see langword="null"/>.</returns>
    private static IMethodSymbol? FindAsyncDisposeMethod(ImmutableArray<ISymbol> members)
    {
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { Parameters.Length: 0, IsStatic: false, ReturnsVoid: false, DeclaredAccessibility: Accessibility.Public } method)
            {
                return method;
            }
        }

        return null;
    }

    /// <summary>Returns whether the type has the duck-typed enumerator shape <c>foreach</c> disposes by pattern.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type has both a public <c>MoveNext()</c> and a public <c>Current</c>.</returns>
    private static bool IsDuckTypedEnumerator(INamedTypeSymbol type)
    {
        var hasMoveNext = false;
        var hasCurrent = false;
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member is IMethodSymbol { Name: "MoveNext", Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public } method
                && method.ReturnType.SpecialType == SpecialType.System_Boolean)
            {
                hasMoveNext = true;
            }
            else if (member is IPropertySymbol { Name: "Current", DeclaredAccessibility: Accessibility.Public })
            {
                hasCurrent = true;
            }
        }

        return hasMoveNext && hasCurrent;
    }

    /// <summary>Reports SST2316 at the orphaned disposal method.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="method">The disposal method.</param>
    /// <param name="type">The declaring type.</param>
    /// <param name="methodName">The disposal method name.</param>
    /// <param name="interfaceName">The interface the method should sign up to.</param>
    private static void Report(SymbolAnalysisContext context, IMethodSymbol method, INamedTypeSymbol type, string methodName, string interfaceName)
    {
        if (method.Locations is not [var location, ..])
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(InterfaceKey, interfaceName);
        context.ReportDiagnostic(Diagnostic.Create(DesignRules.DisposeWithoutInterface, location, properties, type.Name, methodName, interfaceName));
    }
}
