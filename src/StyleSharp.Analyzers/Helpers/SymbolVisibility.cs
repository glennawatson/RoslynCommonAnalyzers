// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Answers whether a symbol can be reached from another assembly. Rules about the burden a member's shape
/// puts on callers only apply when a caller outside the declaring assembly exists; inside the assembly
/// every caller is rebuilt together, so an internal or private member is the author's to change freely.
/// </summary>
internal static class SymbolVisibility
{
    /// <summary>Returns whether a symbol can be seen from outside the assembly that declares it.</summary>
    /// <param name="symbol">The symbol to test.</param>
    /// <returns><see langword="true"/> when the symbol and every type containing it are visible.</returns>
    /// <remarks>
    /// The walk stops at the enclosing namespace: a symbol is externally visible only when it and every
    /// type on the way out are public or protected. A local function is never externally visible, whatever
    /// the method around it says.
    /// </remarks>
    public static bool IsExternallyVisible(ISymbol symbol)
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
