// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Answers how two bound types relate — through the base-type chain or the implemented interfaces — using
/// only the symbols the caller already holds, so nothing is resolved or allocated.
/// </summary>
internal static class TypeRelations
{
    /// <summary>Returns whether a type is, or derives from, another type.</summary>
    /// <param name="type">The type to test, or <see langword="null"/>.</param>
    /// <param name="baseType">The type to find in the base chain, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="baseType"/> is <paramref name="type"/> or one of its base types.</returns>
    /// <remarks>Only the base-type chain is walked; an interface is never found this way.</remarks>
    internal static bool IsOrDerivesFrom(ITypeSymbol? type, ITypeSymbol? baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is, or derives from, any of several types.</summary>
    /// <param name="type">The type to test, or <see langword="null"/>.</param>
    /// <param name="baseTypes">The types to find in the base chain.</param>
    /// <returns><see langword="true"/> when one of <paramref name="baseTypes"/> is in the base chain of <paramref name="type"/>.</returns>
    internal static bool IsOrDerivesFromAny(ITypeSymbol? type, ReadOnlySpan<INamedTypeSymbol> baseTypes)
    {
        if (baseTypes.IsEmpty)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsOneOf(current, baseTypes))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is, or implements, an interface.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="interfaceType">The interface to look for.</param>
    /// <returns><see langword="true"/> when the type is the interface or carries it.</returns>
    /// <remarks>
    /// A type's <c>AllInterfaces</c> does not include the type itself, so the interface is matched directly as
    /// well. That matters where a value is typed as the interface — <c>IDisposable Start()</c>.
    /// </remarks>
    internal static bool IsOrImplements(ITypeSymbol type, ITypeSymbol interfaceType) =>
        SymbolEqualityComparer.Default.Equals(type, interfaceType) || Implements(type, interfaceType);

    /// <summary>Returns whether a type implements an interface, directly or transitively.</summary>
    /// <param name="type">The implementing type.</param>
    /// <param name="interfaceType">The interface to look for.</param>
    /// <returns><see langword="true"/> when the interface is in the type's implemented set.</returns>
    /// <remarks>The interface itself does not implement itself; use <see cref="IsOrImplements"/> when it should count.</remarks>
    internal static bool Implements(ITypeSymbol type, ITypeSymbol interfaceType)
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

    /// <summary>Returns whether a symbol is exactly one of several symbols.</summary>
    /// <param name="symbol">The symbol to test, or <see langword="null"/>.</param>
    /// <param name="candidates">The symbols to compare against.</param>
    /// <returns><see langword="true"/> when a candidate equals <paramref name="symbol"/>.</returns>
    internal static bool IsOneOf(ISymbol? symbol, ReadOnlySpan<INamedTypeSymbol> candidates)
    {
        for (var i = 0; i < candidates.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, candidates[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether something outside a method fixes its signature: an interface member it implicitly implements, or the attribute it constructs.</summary>
    /// <param name="method">The method whose signature a rule wants to change.</param>
    /// <returns><see langword="true"/> when changing a parameter would break a contract the method's own declaration does not show.</returns>
    /// <remarks>
    /// An attribute's constructor is included because every use of the attribute is a call with a fixed shape,
    /// and none of those uses are visible from the constructor's declaration.
    /// </remarks>
    internal static bool IsSignatureBoundByContract(IMethodSymbol method) =>
        method.ContainingType is { } containingType
            && (IsAttributeType(containingType) || ImplementsInterfaceMember(method, containingType));

    /// <summary>Returns whether a type derives from <see cref="Attribute"/>.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for an attribute class.</returns>
    internal static bool IsAttributeType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current is { Name: "Attribute", ContainingNamespace.Name: "System" })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method implicitly implements a member of an interface its type carries.</summary>
    /// <param name="method">The method to test.</param>
    /// <param name="containingType">The method's containing type.</param>
    /// <returns><see langword="true"/> when a same-named interface member maps to <paramref name="method"/>.</returns>
    /// <remarks>An explicit implementation is named for its interface (<c>IShape.Draw</c>), so it is not matched here.</remarks>
    internal static bool ImplementsInterfaceMember(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(method.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidates[j]), method))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
