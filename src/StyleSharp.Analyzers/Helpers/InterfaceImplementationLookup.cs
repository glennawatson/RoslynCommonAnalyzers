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
    public static bool ImplementsInterfaceMember(ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        if (containingType is null || containingType.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(symbol.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                var implementation = containingType.FindImplementationForInterfaceMember(candidates[j]);
                if (SymbolEqualityComparer.Default.Equals(implementation, symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
