// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a params overload (SST2467) that a more specific same-type sibling silently bypasses. A params
/// array parameter is a catch-all, so a same-named overload of the same arity whose last parameter is a
/// stricter type than the array's element type wins overload resolution for every call passing exactly that
/// type — an exact match beats the conversion the params expanded form needs — and a call meant for the params
/// overload quietly runs the sibling instead.
/// </summary>
/// <remarks>
/// <para>
/// The exact reported shape is a declaration-level pair inside one type, decided without looking at any call
/// site: a params method <c>M(..., params E[])</c> of arity <c>n</c>, and a non-params sibling <c>M</c> of the
/// same arity <c>n</c> and the same static-ness, whose leading <c>n - 1</c> parameters have identical types and
/// ref kinds, and whose final by-value parameter type <c>T</c> is strictly more specific than the element type
/// <c>E</c> — a proper subclass of it, an interface it implements, or a value type that boxes to it. In that
/// shape a single trailing argument of static type <c>T</c> binds to the sibling, not the params array's
/// expanded form, because an identity match on <c>T</c> is a better conversion than <c>T</c>-to-<c>E</c>.
/// </para>
/// <para>
/// The sibling whose last parameter is <c>E</c> itself is deliberately <b>not</b> reported: that is the ordinary
/// allocation-avoiding overload (the same behaviour without allocating a one-element array), the compiler is
/// silent on it, and flagging it would be noise. A sibling whose last parameter is a <i>base</i> type of
/// <c>E</c> is not reported either, because the params overload — not the sibling — wins for a value of type
/// <c>E</c>, so nothing is bypassed. Only the strictly-more-specific direction is the surprise.
/// </para>
/// <para>
/// The clean path is symbol-only and allocation-light: a type's members are scanned once, and the sibling
/// lookup runs only for a method that actually ends in a params array. There is no syntax walk and nothing is
/// bound, so a type with no params overload pays a single member scan and returns.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2467BypassedParamsOverloadAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.BypassedParamsOverload);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Examines a type's params overloads for a more specific sibling that bypasses them.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
        {
            return;
        }

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { MethodKind: MethodKind.Ordinary, Arity: 0, IsImplicitlyDeclared: false } method
                && GetParamsElementType(method) is { } elementType)
            {
                AnalyzeParamsMethod(context, type, method, elementType);
            }
        }
    }

    /// <summary>Reports the params method when a strictly more specific same-arity sibling bypasses it.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The declaring type.</param>
    /// <param name="paramsMethod">The params overload under test.</param>
    /// <param name="elementType">The params array's element type.</param>
    private static void AnalyzeParamsMethod(SymbolAnalysisContext context, INamedTypeSymbol type, IMethodSymbol paramsMethod, ITypeSymbol elementType)
    {
        var arity = paramsMethod.Parameters.Length;
        var candidates = type.GetMembers(paramsMethod.Name);
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is not IMethodSymbol { MethodKind: MethodKind.Ordinary, Arity: 0, IsImplicitlyDeclared: false } sibling
                || SymbolEqualityComparer.Default.Equals(sibling, paramsMethod)
                || sibling.IsStatic != paramsMethod.IsStatic
                || sibling.Parameters.Length != arity)
            {
                continue;
            }

            var capturing = sibling.Parameters[arity - 1];
            if (capturing.IsParams
                || capturing.RefKind != RefKind.None
                || !LeadingParametersMatch(paramsMethod, sibling, arity - 1)
                || !IsMoreSpecific(elementType, capturing.Type))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.BypassedParamsOverload,
                paramsMethod.Locations[0],
                paramsMethod.Name,
                capturing.Type.ToDisplayString()));
            return;
        }
    }

    /// <summary>Returns a method's params array element type, or <see langword="null"/> when it takes no params array.</summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>The element type of the trailing params array, or <see langword="null"/>.</returns>
    private static ITypeSymbol? GetParamsElementType(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        if (parameters.Length == 0)
        {
            return null;
        }

        var last = parameters[parameters.Length - 1];
        return last is { IsParams: true, Type: IArrayTypeSymbol array } ? array.ElementType : null;
    }

    /// <summary>Returns whether the two methods share identical leading parameters up to a count.</summary>
    /// <param name="paramsMethod">The params overload.</param>
    /// <param name="sibling">The candidate sibling overload.</param>
    /// <param name="count">The number of leading parameters that must match.</param>
    /// <returns><see langword="true"/> when every leading parameter has the same ref kind and type.</returns>
    private static bool LeadingParametersMatch(IMethodSymbol paramsMethod, IMethodSymbol sibling, int count)
    {
        var paramsParameters = paramsMethod.Parameters;
        var siblingParameters = sibling.Parameters;
        for (var i = 0; i < count; i++)
        {
            if (paramsParameters[i].RefKind != siblingParameters[i].RefKind
                || !SymbolEqualityComparer.Default.Equals(paramsParameters[i].Type, siblingParameters[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a value of <paramref name="derived"/> implicitly converts to a strictly more general <paramref name="general"/>.</summary>
    /// <param name="general">The params array's element type.</param>
    /// <param name="derived">The sibling's final parameter type.</param>
    /// <returns><see langword="true"/> when <paramref name="derived"/> is a proper subclass of, boxes to, or implements <paramref name="general"/>.</returns>
    private static bool IsMoreSpecific(ITypeSymbol general, ITypeSymbol derived)
    {
        if (SymbolEqualityComparer.Default.Equals(general, derived))
        {
            return false;
        }

        // 'object' is the universal params catch-all: every other type — including an interface or a type
        // parameter, whose base-chain walk would not reach 'object' — is a strictly more specific match for a
        // single argument, so any non-'object' sibling of the same arity bypasses a 'params object[]' overload.
        if (general.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        for (var current = derived.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, general))
            {
                return true;
            }
        }

        return general.TypeKind == TypeKind.Interface && Implements(derived, general);
    }

    /// <summary>Returns whether a type implements a given interface directly or transitively.</summary>
    /// <param name="type">The implementing type.</param>
    /// <param name="interfaceType">The interface to look for.</param>
    /// <returns><see langword="true"/> when the interface is in the type's implemented set.</returns>
    private static bool Implements(ITypeSymbol type, ITypeSymbol interfaceType)
    {
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
}
