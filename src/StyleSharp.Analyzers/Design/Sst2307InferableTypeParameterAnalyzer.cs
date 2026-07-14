// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a type parameter on an externally visible generic method that appears in none of the method's
/// parameters (SST2307). C# infers type arguments from the arguments a call supplies and from nothing
/// else, so such a type parameter can never be inferred and every call site has to name it.
/// </summary>
/// <remarks>
/// <para>
/// The return type does not participate in inference, so <c>T Create&lt;T&gt;()</c> is reported even though
/// <c>T</c> is written in the signature: the caller still has to say <c>Create&lt;Order&gt;()</c>. Neither do
/// constraints, so <c>void Copy&lt;TSource, TItem&gt;(TSource source) where TSource : IEnumerable&lt;TItem&gt;</c>
/// is reported on <c>TItem</c> — the compiler will not walk the constraint to find it.
/// </para>
/// <para>
/// A type parameter counts as inferable wherever it appears inside a parameter's type, however deeply:
/// <c>T[]</c>, <c>List&lt;T&gt;</c>, <c>Func&lt;int, T&gt;</c>, <c>(T, string)</c>, <c>ref T</c> and an
/// extension method's receiver all pin it down.
/// </para>
/// <para>
/// A method whose signature its author cannot change is never reported: an <c>override</c>, an explicit
/// interface implementation, and a method that implements an interface member all inherit their shape from
/// somewhere else, and the fix belongs at that declaration rather than here. The interface or base
/// declaration itself is still reported, which is the one place changing it does something.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2307InferableTypeParameterAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.InferableTypeParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    /// <summary>Reports each type parameter a caller of this method would have to name.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <remarks>
    /// The cheap tests come first: a method with no type parameters — nearly all of them — costs one array
    /// length check, and the interface-implementation walk is only paid once a type parameter has already
    /// failed inference.
    /// </remarks>
    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.TypeParameters.IsEmpty
            || method.MethodKind != MethodKind.Ordinary
            || method.IsOverride
            || !method.ExplicitInterfaceImplementations.IsEmpty
            || !IsExternallyVisible(method))
        {
            return;
        }

        var typeParameters = method.TypeParameters;
        var parameters = method.Parameters;
        if (!HasUninferableTypeParameter(typeParameters, parameters) || ImplementsAnInterfaceMember(method))
        {
            return;
        }

        for (var i = 0; i < typeParameters.Length; i++)
        {
            var typeParameter = typeParameters[i];
            if (AppearsInAnyParameter(parameters, typeParameter))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DesignRules.InferableTypeParameter,
                typeParameter.Locations[0],
                typeParameter.Name,
                method.Name));
        }
    }

    /// <summary>Returns whether any of the method's type parameters fails inference.</summary>
    /// <param name="typeParameters">The method's type parameters.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <returns><see langword="true"/> when at least one type parameter appears in no parameter.</returns>
    /// <remarks>
    /// This runs before the interface-implementation walk so that a compliant method — the overwhelming
    /// majority — never pays for it.
    /// </remarks>
    private static bool HasUninferableTypeParameter(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        ImmutableArray<IParameterSymbol> parameters)
    {
        for (var i = 0; i < typeParameters.Length; i++)
        {
            if (!AppearsInAnyParameter(parameters, typeParameters[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type parameter appears anywhere in the method's parameter list.</summary>
    /// <param name="parameters">The method's parameters.</param>
    /// <param name="typeParameter">The type parameter to look for.</param>
    /// <returns><see langword="true"/> when at least one parameter's type mentions it.</returns>
    private static bool AppearsInAnyParameter(ImmutableArray<IParameterSymbol> parameters, ITypeParameterSymbol typeParameter)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (AppearsIn(parameters[i].Type, typeParameter))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type mentions a type parameter, at any depth.</summary>
    /// <param name="type">The type to search.</param>
    /// <param name="typeParameter">The type parameter to look for.</param>
    /// <returns><see langword="true"/> when the type is, or is built over, the type parameter.</returns>
    /// <remarks>
    /// A constructed type's arguments are walked because inference sees through them: <c>List&lt;T&gt;</c>
    /// pins <c>T</c> down exactly as <c>T</c> itself does. A type parameter's own constraints are not
    /// walked, because the compiler does not use them to infer.
    /// </remarks>
    private static bool AppearsIn(ITypeSymbol type, ITypeParameterSymbol typeParameter)
    {
        switch (type)
        {
            case ITypeParameterSymbol candidate:
            {
                return SymbolEqualityComparer.Default.Equals(candidate, typeParameter);
            }

            case IArrayTypeSymbol array:
            {
                return AppearsIn(array.ElementType, typeParameter);
            }

            case IPointerTypeSymbol pointer:
            {
                return AppearsIn(pointer.PointedAtType, typeParameter);
            }

            case INamedTypeSymbol named:
            {
                var arguments = named.TypeArguments;
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (AppearsIn(arguments[i], typeParameter))
                    {
                        return true;
                    }
                }

                return false;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Returns whether a method implements an interface member, so its shape is not its own.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><see langword="true"/> when some interface member maps to this method.</returns>
    /// <remarks>
    /// Explicit implementations are already gone by the time this runs; this catches the implicit ones,
    /// where the signature is dictated by an interface the author may not even own. Reporting there would
    /// demand a change that cannot be made without breaking the contract.
    /// </remarks>
    private static bool ImplementsAnInterfaceMember(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var members = interfaces[i].GetMembers(method.Name);
            for (var j = 0; j < members.Length; j++)
            {
                if (members[j] is not IMethodSymbol interfaceMethod)
                {
                    continue;
                }

                var implementation = containingType.FindImplementationForInterfaceMember(interfaceMethod);
                if (SymbolEqualityComparer.Default.Equals(implementation, method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a symbol can be seen from outside the assembly that declares it.</summary>
    /// <param name="symbol">The symbol to test.</param>
    /// <returns><see langword="true"/> when the symbol and every type containing it are visible.</returns>
    /// <remarks>
    /// The rule is about the burden a signature puts on its callers, and an internal method's callers are
    /// all in the assembly that can change it freely. A local function is never externally visible, whatever
    /// the method around it says.
    /// </remarks>
    private static bool IsExternallyVisible(ISymbol symbol)
    {
        for (var current = symbol; current is not null && current.Kind != SymbolKind.Namespace; current = current.ContainingSymbol)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                {
                    break;
                }

                default:
                {
                    return false;
                }
            }
        }

        return true;
    }
}
