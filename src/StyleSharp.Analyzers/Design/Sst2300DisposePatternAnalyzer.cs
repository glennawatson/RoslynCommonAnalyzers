// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a class that implements <c>IDisposable</c> but builds only half of the disposal pattern
/// (SST2300).
/// </summary>
/// <remarks>
/// <para>
/// On the type that first signs the contract, four things are checked, in this order, and the first one
/// that fails is the one reported — the clauses overlap enough that stacking four squiggles on one type
/// would say the same thing four times:
/// </para>
/// <list type="number">
/// <item><description>an unsealed type that declares no <c>Dispose(bool)</c> for a derived type to override;</description></item>
/// <item><description>a <c>Dispose(bool)</c> that is public, which puts the finalizer's own switch on the type's API;</description></item>
/// <item><description>a <c>Dispose()</c> that never chains to <c>Dispose(true)</c>, so the cleanup the pattern moved never runs;</description></item>
/// <item><description>a finalizable type whose <c>Dispose()</c> forgets <c>GC.SuppressFinalize(this)</c>, which costs every instance a trip through the finalizer queue.</description></item>
/// </list>
/// <para>
/// A type whose base class already implements <c>IDisposable</c> inherits the pattern and is checked for
/// one thing only: a new parameterless <c>Dispose()</c> that <b>hides</b> the base's. That hidden method
/// never runs from a <c>using</c> on a base-typed variable, which dispatches statically to the base's
/// <c>Dispose()</c> — so the derived cleanup silently does not happen. An <c>override</c> of a virtual base
/// <c>Dispose()</c> is correct and not reported.
/// </para>
/// <para>
/// A <b>sealed class with no finalizer</b> is deliberately never reported. Nothing can derive from it
/// and nothing else will call its cleanup, so a plain <c>Dispose()</c> is the whole correct pattern and
/// demanding the ceremony would be noise. Records are skipped, and so is a type that implements only
/// <c>IAsyncDisposable</c> — asynchronous disposal is a different pattern and out of this rule's scope.
/// </para>
/// <para>
/// The clean path is one walk of the interface list, so a type that is not disposable costs a loop over
/// an array that is almost always empty. Only a disposable type reaches the member scan, and only a
/// disposable type with a body worth reading reaches the syntax walk.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2300DisposePatternAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key naming the failing clause a code fix can repair.</summary>
    internal const string ClauseKey = "DisposeClause";

    /// <summary>The <see cref="ClauseKey"/> value for a <c>Dispose()</c> that does not suppress finalization.</summary>
    internal const string SuppressFinalizeClause = "SuppressFinalize";

    /// <summary>The <see cref="ClauseKey"/> value for a <c>Dispose(bool)</c> that is public.</summary>
    internal const string PublicDisposeBoolClause = "PublicDisposeBool";

    /// <summary>The diagnostic property key carrying the modifiers a public <c>Dispose(bool)</c> should declare instead.</summary>
    internal const string ReplacementModifiersKey = "ReplacementModifiers";

    /// <summary>The modifiers a <c>Dispose(bool)</c> declares on a type that can still be derived from.</summary>
    internal const string OverridableModifiers = "protected virtual";

    /// <summary>The modifiers a <c>Dispose(bool)</c> declares on a sealed type, which nothing can override.</summary>
    internal const string SealedModifiers = "private";

    /// <summary>The name of the parameterless disposal method, and of its overload.</summary>
    private const string DisposeName = "Dispose";

    /// <summary>The suffix an explicitly implemented <c>IDisposable.Dispose</c> carries in its metadata name.</summary>
    private const string ExplicitDisposeSuffix = ".Dispose";

    /// <summary>The method that takes an instance off the finalizer queue.</summary>
    private const string SuppressFinalizeName = "SuppressFinalize";

    /// <summary>The metadata name of the type that owns the finalizer queue.</summary>
    private const string GarbageCollectorMetadataName = "System.GC";

    /// <summary>The cached properties for a <c>Dispose()</c> that does not suppress finalization.</summary>
    private static readonly ImmutableDictionary<string, string?> SuppressFinalizeProperties =
        ImmutableDictionary<string, string?>.Empty.Add(ClauseKey, SuppressFinalizeClause);

    /// <summary>The cached properties for a public <c>Dispose(bool)</c> on a type that can be derived from.</summary>
    private static readonly ImmutableDictionary<string, string?> OverridableDisposeBoolProperties =
        ImmutableDictionary<string, string?>.Empty.Add(ClauseKey, PublicDisposeBoolClause).Add(ReplacementModifiersKey, OverridableModifiers);

    /// <summary>The cached properties for a public <c>Dispose(bool)</c> on a sealed type.</summary>
    private static readonly ImmutableDictionary<string, string?> SealedDisposeBoolProperties =
        ImmutableDictionary<string, string?>.Empty.Add(ClauseKey, PublicDisposeBoolClause).Add(ReplacementModifiersKey, SealedModifiers);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.DisposePattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Checks one type against the disposal pattern and reports the first clause it fails.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsRecord || type.IsStatic)
        {
            return;
        }

        if (!ImplementsDisposable(type))
        {
            return;
        }

        // The pattern is owned by the type that first signs the contract. A type whose base is already
        // disposable inherits it and only overrides Dispose(bool) — unless it re-declares a parameterless
        // Dispose() that hides the base's, in which case a `using` on a base-typed variable calls the wrong one.
        if (ImplementsDisposable(type.BaseType))
        {
            ReportHiddenDispose(context, type);
            return;
        }

        var members = FindDisposeMembers(type);
        if (!type.IsSealed && members.DisposeBool is null)
        {
            ReportOnType(context, type, "does not declare 'protected virtual void Dispose(bool disposing)'");
            return;
        }

        if (members.DisposeBool is { DeclaredAccessibility: Accessibility.Public } disposeBool)
        {
            ReportPublicDisposeBool(context, type, disposeBool);
            return;
        }

        AnalyzeDisposeBody(context, type, members);
    }

    /// <summary>Reports a derived type that hides the base's <c>Dispose()</c> with a new parameterless one.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The disposable type whose base already owns the pattern.</param>
    /// <remarks>
    /// The base is disposable, so it has a <c>Dispose()</c>. A public, non-override, non-explicit
    /// parameterless <c>Dispose()</c> on the derived type therefore hides it: static dispatch on a
    /// base-typed variable — the <c>using</c> case — never reaches this one. An <c>override</c> (of a
    /// virtual base <c>Dispose()</c>) is correct and not reported; nor is an explicit
    /// <c>IDisposable.Dispose</c>, which does not shadow the public member.
    /// </remarks>
    private static void ReportHiddenDispose(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        var members = type.GetMembers(DisposeName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol method && HidesBaseDispose(method))
            {
                Report(
                    context,
                    method.Locations[0],
                    type,
                    "hides the base type's 'Dispose()' with a new one; a 'using' on the base type calls the base's, not this one",
                    null);
                return;
            }
        }
    }

    /// <summary>Returns whether a method is a new parameterless <c>Dispose()</c> that hides the base's.</summary>
    /// <param name="method">A member named <c>Dispose</c>.</param>
    /// <returns><see langword="true"/> for a public, non-override, non-explicit, parameterless <c>Dispose()</c> declared in source.</returns>
    private static bool HidesBaseDispose(IMethodSymbol method)
        => method is { IsOverride: false, IsStatic: false, ReturnsVoid: true, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 0 }
            && method.ExplicitInterfaceImplementations.Length == 0
            && method.Locations.Length > 0
            && method.Locations[0].IsInSource;

    /// <summary>Reads what the type's <c>Dispose()</c> body does and reports the first thing it omits.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The disposable type.</param>
    /// <param name="members">The type's disposal members.</param>
    /// <remarks>
    /// A <c>Dispose()</c> with no body — an abstract declaration, or one that lives in metadata — states
    /// nothing to disagree with, so nothing is reported for it.
    /// </remarks>
    private static void AnalyzeDisposeBody(SymbolAnalysisContext context, INamedTypeSymbol type, in DisposeMembers members)
    {
        if (members.Dispose is null
            || GetDeclaration(members.Dispose, context.CancellationToken) is not { } declaration
            || (declaration.Body is null && declaration.ExpressionBody is null))
        {
            return;
        }

        var calls = FindDisposeCalls(declaration);
        if (members.DisposeBool is not null && !calls.ChainsToDisposeTrue)
        {
            Report(context, declaration.Identifier.GetLocation(), type, "declares 'Dispose(bool)' but its 'Dispose()' never calls 'Dispose(true)'", null);
            return;
        }

        // The suggestion names an API, so it is only made once that API is known to be there. The lookup
        // sits behind every other test: a compilation with no finalizable disposable type never runs it.
        if (!members.HasFinalizer || calls.SuppressesFinalize || !CanSuppressFinalize(context.Compilation))
        {
            return;
        }

        Report(
            context,
            declaration.Identifier.GetLocation(),
            type,
            "has a finalizer but its 'Dispose()' never calls 'GC.SuppressFinalize(this)'",
            SuppressFinalizeProperties);
    }

    /// <summary>Returns whether the analyzed compilation actually has the call the rule is about to ask for.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns><see langword="true"/> when <c>GC.SuppressFinalize(object)</c> can be bound.</returns>
    private static bool CanSuppressFinalize(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(GarbageCollectorMetadataName) is not { } garbageCollector)
        {
            return false;
        }

        var members = garbageCollector.GetMembers(SuppressFinalizeName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true, Parameters.Length: 1 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports a <c>Dispose(bool)</c> that anyone can call.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The disposable type.</param>
    /// <param name="disposeBool">The public <c>Dispose(bool)</c> overload.</param>
    private static void ReportPublicDisposeBool(SymbolAnalysisContext context, INamedTypeSymbol type, IMethodSymbol disposeBool)
    {
        if (disposeBool.Locations.Length == 0 || !disposeBool.Locations[0].IsInSource)
        {
            return;
        }

        var replacement = type.IsSealed ? SealedModifiers : OverridableModifiers;
        var properties = type.IsSealed ? SealedDisposeBoolProperties : OverridableDisposeBoolProperties;
        Report(
            context,
            disposeBool.Locations[0],
            type,
            $"declares 'Dispose(bool)' as public rather than '{replacement}'",
            properties);
    }

    /// <summary>Reports a clause against the type's own declaration.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The disposable type.</param>
    /// <param name="clause">The clause the type fails.</param>
    private static void ReportOnType(SymbolAnalysisContext context, INamedTypeSymbol type, string clause)
    {
        if (type.Locations.Length == 0 || !type.Locations[0].IsInSource)
        {
            return;
        }

        Report(context, type.Locations[0], type, clause, null);
    }

    /// <summary>Reports one failed clause.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="location">Where the clause fails.</param>
    /// <param name="type">The disposable type.</param>
    /// <param name="clause">The clause the type fails.</param>
    /// <param name="properties">The properties a code fix reads, when the clause has one.</param>
    private static void Report(
        SymbolAnalysisContext context,
        Location location,
        INamedTypeSymbol type,
        string clause,
        ImmutableDictionary<string, string?>? properties)
        => context.ReportDiagnostic(Diagnostic.Create(
            DesignRules.DisposePattern,
            location,
            properties,
            type.Name,
            clause));

    /// <summary>Returns whether a type implements <c>IDisposable</c>.</summary>
    /// <param name="type">The type to test, which may be null for the base of <c>object</c>.</param>
    /// <returns><see langword="true"/> when the type signs the disposal contract.</returns>
    private static bool ImplementsDisposable(INamedTypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (interfaces[i].SpecialType == SpecialType.System_IDisposable)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the members the disposal pattern is made of.</summary>
    /// <param name="type">The disposable type.</param>
    /// <returns>The type's <c>Dispose()</c>, its <c>Dispose(bool)</c>, and whether it has a finalizer.</returns>
    private static DisposeMembers FindDisposeMembers(INamedTypeSymbol type)
    {
        IMethodSymbol? dispose = null;
        IMethodSymbol? disposeBool = null;
        var hasFinalizer = false;

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IMethodSymbol method)
            {
                continue;
            }

            if (method.MethodKind == MethodKind.Destructor)
            {
                hasFinalizer = true;
            }
            else if (IsDisposeOverload(method))
            {
                disposeBool = method;
            }
            else if (IsDispose(method))
            {
                dispose = method;
            }
        }

        return new DisposeMembers(dispose, disposeBool, hasFinalizer);
    }

    /// <summary>Returns whether a method is the <c>IDisposable.Dispose()</c> implementation.</summary>
    /// <param name="method">The candidate method.</param>
    /// <returns><see langword="true"/> for a parameterless <c>Dispose()</c>, explicit or not.</returns>
    private static bool IsDispose(IMethodSymbol method)
        => method.Parameters.Length == 0
            && method.ReturnsVoid
            && (string.Equals(method.Name, DisposeName, StringComparison.Ordinal)
                || method.Name.EndsWith(ExplicitDisposeSuffix, StringComparison.Ordinal));

    /// <summary>Returns whether a method is the pattern's <c>Dispose(bool)</c> overload.</summary>
    /// <param name="method">The candidate method.</param>
    /// <returns><see langword="true"/> for a <c>Dispose(bool)</c>.</returns>
    private static bool IsDisposeOverload(IMethodSymbol method)
        => method.Parameters.Length == 1
            && method.ReturnsVoid
            && string.Equals(method.Name, DisposeName, StringComparison.Ordinal)
            && method.Parameters[0].Type.SpecialType == SpecialType.System_Boolean;

    /// <summary>Gets the source declaration of a method.</summary>
    /// <param name="method">The method to locate.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The declaration, or <see langword="null"/> when the method has no source.</returns>
    private static MethodDeclarationSyntax? GetDeclaration(IMethodSymbol method, CancellationToken cancellationToken)
    {
        var references = method.DeclaringSyntaxReferences;
        return references.Length == 0
            ? null
            : references[0].GetSyntax(cancellationToken) as MethodDeclarationSyntax;
    }

    /// <summary>Reads which of the pattern's two calls a <c>Dispose()</c> body makes.</summary>
    /// <param name="declaration">The <c>Dispose()</c> declaration.</param>
    /// <returns>The calls the body was found to make.</returns>
    /// <remarks>
    /// Matched on syntax: the calls the pattern asks for are written the same way everywhere, and a bind
    /// would cost a semantic model to learn nothing the shape does not already say. A call nested inside a
    /// guard or a helper block still counts, so the walk covers the whole declaration.
    /// </remarks>
    private static DisposeCalls FindDisposeCalls(MethodDeclarationSyntax declaration)
    {
        var calls = default(DisposeCalls);
        DescendantTraversalHelper.VisitDescendants<InvocationExpressionSyntax, DisposeCalls>(declaration, ref calls, MatchDisposeCall);
        return calls;
    }

    /// <summary>Records an invocation that is one of the pattern's two calls.</summary>
    /// <param name="invocation">The visited invocation.</param>
    /// <param name="calls">The scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once both calls are found.</returns>
    private static bool MatchDisposeCall(InvocationExpressionSyntax invocation, ref DisposeCalls calls)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != 1)
        {
            return true;
        }

        var name = GetInvokedName(invocation.Expression);
        if (string.Equals(name, DisposeName, StringComparison.Ordinal))
        {
            calls.ChainsToDisposeTrue |= arguments[0].Expression.IsKind(SyntaxKind.TrueLiteralExpression);
        }
        else if (string.Equals(name, SuppressFinalizeName, StringComparison.Ordinal))
        {
            calls.SuppressesFinalize |= arguments[0].Expression.IsKind(SyntaxKind.ThisExpression);
        }

        return !calls.ChainsToDisposeTrue || !calls.SuppressesFinalize;
    }

    /// <summary>Gets the simple name an invocation calls.</summary>
    /// <param name="expression">The invoked expression.</param>
    /// <returns>The name, or an empty string when the call is not a simple or member-access call.</returns>
    private static string GetInvokedName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>The members a disposable type's pattern is assembled from.</summary>
    /// <param name="Dispose">The parameterless <c>Dispose()</c>, when the type declares one.</param>
    /// <param name="DisposeBool">The <c>Dispose(bool)</c> overload, when the type declares one.</param>
    /// <param name="HasFinalizer">Whether the type declares a finalizer.</param>
    private readonly record struct DisposeMembers(IMethodSymbol? Dispose, IMethodSymbol? DisposeBool, bool HasFinalizer);

    /// <summary>Mutable accumulator for the scan of a <c>Dispose()</c> body.</summary>
    private record struct DisposeCalls
    {
        /// <summary>Gets or sets a value indicating whether the body calls <c>Dispose(true)</c>.</summary>
        public bool ChainsToDisposeTrue { get; set; }

        /// <summary>Gets or sets a value indicating whether the body calls <c>GC.SuppressFinalize(this)</c>.</summary>
        public bool SuppressesFinalize { get; set; }
    }
}
