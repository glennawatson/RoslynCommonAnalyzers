// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Reads what a bound symbol carries — its attributes, its members, the syntax that declares it — without allocating.</summary>
internal static class SymbolFacts
{
    /// <summary>Returns whether any attribute's class is exactly the marker.</summary>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="marker">The attribute class to find.</param>
    /// <returns><see langword="true"/> when an attribute is bound to <paramref name="marker"/>.</returns>
    /// <remarks>A class derived from the marker does not count; use <see cref="HasAttributeDerivedFromAny"/> for that.</remarks>
    internal static bool HasAttribute(ImmutableArray<AttributeData> attributes, INamedTypeSymbol marker)
    {
        for (var i = 0; i < attributes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, marker))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any attribute's class is, or derives from, one of the markers.</summary>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="markers">The resolved marker attribute classes.</param>
    /// <returns><see langword="true"/> when an attribute matches a marker.</returns>
    internal static bool HasAttributeDerivedFromAny(ImmutableArray<AttributeData> attributes, INamedTypeSymbol[] markers)
    {
        if (markers.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < attributes.Length; i++)
        {
            if (TypeRelations.IsOrDerivesFromAny(attributes[i].AttributeClass, markers))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type declares at least one method with the given name.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when a method of that name is declared on <paramref name="type"/>.</returns>
    /// <remarks>Inherited members are not searched; a property or field with the name does not count.</remarks>
    internal static bool HasMethodNamed(INamedTypeSymbol type, string name)
    {
        var members = type.GetMembers(name);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any of a symbol's declarations is written as the given syntax.</summary>
    /// <typeparam name="TSyntax">The declaring syntax to look for.</typeparam>
    /// <param name="symbol">The symbol whose declarations are inspected.</param>
    /// <param name="cancellationToken">A token that cancels realizing the declaring syntax.</param>
    /// <returns><see langword="true"/> when a declaring reference realizes as <typeparamref name="TSyntax"/>.</returns>
    internal static bool IsDeclaredAs<TSyntax>(ISymbol symbol, CancellationToken cancellationToken)
        where TSyntax : SyntaxNode
    {
        var references = symbol.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax(cancellationToken) is TSyntax)
            {
                return true;
            }
        }

        return false;
    }
}
