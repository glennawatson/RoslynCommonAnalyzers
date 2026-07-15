// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method (SST2427) whose parameter is a base type of a same-named base-class overload's
/// parameter. Overload resolution stops at the first type in the hierarchy with an applicable member, so
/// the more general derived overload is chosen for every call the base overload would have handled, and the
/// base overload is never reached through the derived type.
/// </summary>
/// <remarks>
/// The clean path is cheap: a type whose base is <c>object</c> is rejected outright, and for every other
/// type the walk starts from a declared method's own parameters and only climbs the base chain looking for
/// a same-named, same-arity method. An <c>override</c> is rejected from its symbol, and a member the author
/// marked <c>new</c> — the acknowledged intent to hide — is read from its declaration only once a hiding
/// relationship has actually been found, so the syntax is never fetched on the clean path.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2427HidingGeneralOverloadAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.HidingGeneralOverload);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Examines a class's own methods for one that hides a more specific base overload.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.BaseType is null or { SpecialType: SpecialType.System_Object })
        {
            return;
        }

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsOverride: false, Arity: 0, IsImplicitlyDeclared: false } method
                && method.Parameters.Length > 0
                && !HasParamsParameter(method.Parameters))
            {
                AnalyzeMethod(context, type, method);
            }
        }
    }

    /// <summary>Walks the base chain for a same-named overload this method is general enough to hide.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The declaring type.</param>
    /// <param name="method">The declared method under test.</param>
    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol type, IMethodSymbol method)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            var candidates = baseType.GetMembers(method.Name);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is not IMethodSymbol { MethodKind: MethodKind.Ordinary, Arity: 0 } baseMethod
                    || baseMethod.DeclaredAccessibility == Accessibility.Private
                    || baseMethod.Parameters.Length != method.Parameters.Length
                    || HasParamsParameter(baseMethod.Parameters))
                {
                    continue;
                }

                if (!TryFindGeneralizedParameter(method, baseMethod, out var index))
                {
                    continue;
                }

                // A member marked 'new' is the author acknowledging the hide, so it is left alone — and the
                // declaration is only read now, on the verge of reporting, never on the clean path.
                if (DeclaresNewModifier(method))
                {
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    CorrectnessRules.HidingGeneralOverload,
                    method.Locations[0],
                    method.Name,
                    method.Parameters[index].Type.ToDisplayString(),
                    baseMethod.Parameters[index].Type.ToDisplayString(),
                    $"{baseMethod.ContainingType.Name}.{baseMethod.Name}"));
                return;
            }
        }
    }

    /// <summary>Returns whether the derived method is applicable wherever the base one is, and strictly more general somewhere.</summary>
    /// <param name="method">The derived method.</param>
    /// <param name="baseMethod">The candidate base method.</param>
    /// <param name="index">The first parameter position at which the derived type is strictly more general.</param>
    /// <returns><see langword="true"/> when every parameter is equal-or-more-general and at least one is strictly more general.</returns>
    private static bool TryFindGeneralizedParameter(IMethodSymbol method, IMethodSymbol baseMethod, out int index)
    {
        index = -1;
        var methodParameters = method.Parameters;
        var baseParameters = baseMethod.Parameters;
        for (var i = 0; i < methodParameters.Length; i++)
        {
            var derivedParameter = methodParameters[i];
            var baseParameter = baseParameters[i];
            if (derivedParameter.RefKind != baseParameter.RefKind)
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(derivedParameter.Type, baseParameter.Type))
            {
                continue;
            }

            // A by-reference argument requires an exact type match, so only a by-value position can be the
            // one that makes the derived overload strictly more general.
            if (derivedParameter.RefKind == RefKind.None && IsProperBaseType(derivedParameter.Type, baseParameter.Type))
            {
                if (index < 0)
                {
                    index = i;
                }

                continue;
            }

            return false;
        }

        return index >= 0;
    }

    /// <summary>Returns whether <paramref name="candidateBase"/> is a proper base class or interface of <paramref name="derived"/>.</summary>
    /// <param name="candidateBase">The possibly-more-general type.</param>
    /// <param name="derived">The possibly-more-specific type.</param>
    /// <returns><see langword="true"/> when a value of <paramref name="derived"/> is implicitly a <paramref name="candidateBase"/>.</returns>
    private static bool IsProperBaseType(ITypeSymbol candidateBase, ITypeSymbol derived)
    {
        if (SymbolEqualityComparer.Default.Equals(candidateBase, derived))
        {
            return false;
        }

        for (var current = derived.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, candidateBase))
            {
                return true;
            }
        }

        return candidateBase.TypeKind == TypeKind.Interface && ImplementsInterface(derived, candidateBase);
    }

    /// <summary>Returns whether a type implements a given interface directly or transitively.</summary>
    /// <param name="type">The implementing type.</param>
    /// <param name="interfaceType">The interface to look for.</param>
    /// <returns><see langword="true"/> when the interface is in the type's implemented set.</returns>
    private static bool ImplementsInterface(ITypeSymbol type, ITypeSymbol interfaceType)
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

    /// <summary>Returns whether any parameter is a <c>params</c> array, whose applicability the rule does not model.</summary>
    /// <param name="parameters">The parameters to test.</param>
    /// <returns><see langword="true"/> when at least one parameter is <c>params</c>.</returns>
    private static bool HasParamsParameter(ImmutableArray<IParameterSymbol> parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].IsParams)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method's declaration carries the <c>new</c> modifier.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><see langword="true"/> when the author declared the method with <c>new</c>.</returns>
    private static bool DeclaresNewModifier(IMethodSymbol method)
    {
        var references = method.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax() is MethodDeclarationSyntax declaration
                && declaration.Modifiers.Any(SyntaxKind.NewKeyword))
            {
                return true;
            }
        }

        return false;
    }
}
