// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Answers whether a member's shape is dictated by an interface the containing type signed. A rule that
/// asks a member to change shape has nothing to say about such a member: the interface is the place the
/// change belongs, and the interface declaration is what gets reported instead.
/// </summary>
internal static class InterfaceImplementationLookup
{
    /// <summary>Returns whether a member implements an interface member of its containing type.</summary>
    /// <param name="symbol">The declared member.</param>
    /// <returns><see langword="true"/> when an interface dictates the member's shape.</returns>
    /// <remarks>
    /// An interface's own members are never "implementations", so a declaration inside an interface is
    /// reported — it is the declaration that owns the shape. The lookup is by name first, so only the
    /// handful of same-named interface members are ever resolved.
    /// </remarks>
    internal static bool ImplementsInterfaceMember(ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        return containingType is not null
            && containingType.TypeKind != TypeKind.Interface
            && FindImplementedInterfaceMember(containingType, symbol) is not null;
    }

    /// <summary>Returns the interface member a member implicitly implements, whatever kind of type declares it.</summary>
    /// <param name="symbol">The declared member.</param>
    /// <returns>The implemented interface member, or <see langword="null"/> when the member implements none.</returns>
    internal static ISymbol? FindImplementedInterfaceMember(ISymbol symbol) =>
        symbol.ContainingType is { } containingType ? FindImplementedInterfaceMember(containingType, symbol) : null;

    /// <summary>Returns the interface member a member of <paramref name="containingType"/> implicitly implements.</summary>
    /// <param name="containingType">The type that declares <paramref name="symbol"/>.</param>
    /// <param name="symbol">The declared member.</param>
    /// <returns>The implemented interface member, or <see langword="null"/> when the member implements none.</returns>
    /// <remarks>
    /// Only same-named interface members of the same symbol kind are resolved: an implementation always
    /// shares both with the member it implements, so every other candidate is skipped without a lookup.
    /// </remarks>
    internal static ISymbol? FindImplementedInterfaceMember(INamedTypeSymbol containingType, ISymbol symbol)
    {
        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(symbol.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                var candidate = candidates[j];
                if (candidate.Kind == symbol.Kind
                    && SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidate), symbol))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
