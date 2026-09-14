// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Reads what a bound symbol carries — its attributes, its members, the syntax that declares it — without allocating.</summary>
internal static class SymbolFacts
{
    /// <summary>The parameter count that matches a method of any arity.</summary>
    private const int AnyParameterCount = -1;

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

    /// <summary>Returns whether any attribute's class has the given name, whatever namespace it is in.</summary>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="name">The attribute class name, including its <c>Attribute</c> suffix.</param>
    /// <returns><see langword="true"/> when an attribute's class is named <paramref name="name"/>.</returns>
    internal static bool HasAttributeNamed(ImmutableArray<AttributeData> attributes, string name)
    {
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass?.Name == name)
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

    /// <summary>Returns whether a type or one of its base types carries an attribute whose class is, or derives from, the marker.</summary>
    /// <param name="type">The type whose own and base-type attributes are inspected.</param>
    /// <param name="marker">The attribute class to find.</param>
    /// <returns><see langword="true"/> when an attribute anywhere in the base chain matches <paramref name="marker"/>.</returns>
    internal static bool HasAttributeDerivedFromInHierarchy(INamedTypeSymbol type, INamedTypeSymbol marker)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var attributes = current.GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                if (TypeRelations.IsOrDerivesFrom(attributes[i].AttributeClass, marker))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a type declares at least one method with the given name.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when a method of that name is declared on <paramref name="type"/>.</returns>
    /// <remarks>Inherited members are not searched; a property or field with the name does not count.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasMethodNamed(INamedTypeSymbol type, string name) =>
        HasMethod(type, name, AnyParameterCount, requireStatic: false, requireOverride: false);

    /// <summary>Returns whether a type declares a static method with the given name.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when at least one static overload of that name is declared.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasStaticMethod(INamedTypeSymbol type, string name) =>
        HasMethod(type, name, AnyParameterCount, requireStatic: true, requireOverride: false);

    /// <summary>Returns whether a type declares a static method with the given name and parameter count.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The method name.</param>
    /// <param name="parameterCount">The parameter count the overload must have.</param>
    /// <returns><see langword="true"/> when a static overload of that name and arity is declared.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasStaticMethod(INamedTypeSymbol type, string name, int parameterCount) =>
        HasMethod(type, name, parameterCount, requireStatic: true, requireOverride: false);

    /// <summary>Returns whether a type declares a static field with the given name.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The field name.</param>
    /// <returns><see langword="true"/> when a static field of that name is declared.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasStaticField(INamedTypeSymbol type, string name) =>
        HasStaticMember(type, name, SymbolKind.Field);

    /// <summary>Returns whether a type declares a static property with the given name.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> when a static property of that name is declared.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasStaticProperty(INamedTypeSymbol type, string name) =>
        HasStaticMember(type, name, SymbolKind.Property);

    /// <summary>Returns whether a type itself declares an override with the given name and parameter count.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The method name.</param>
    /// <param name="parameterCount">The parameter count the override must have.</param>
    /// <returns><see langword="true"/> when the override is declared on <paramref name="type"/> rather than inherited.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasOverride(INamedTypeSymbol type, string name, int parameterCount) =>
        HasMethod(type, name, parameterCount, requireStatic: false, requireOverride: true);

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

    /// <summary>Returns whether a type declares a method of the given name that meets the requirements.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The method name.</param>
    /// <param name="parameterCount">The required parameter count, or <see cref="AnyParameterCount"/> for any.</param>
    /// <param name="requireStatic">Whether the method must be static.</param>
    /// <param name="requireOverride">Whether the method must be an override.</param>
    /// <returns><see langword="true"/> when a matching method is declared.</returns>
    private static bool HasMethod(INamedTypeSymbol type, string name, int parameterCount, bool requireStatic, bool requireOverride)
    {
        var members = type.GetMembers(name);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol method
                && (!requireStatic || method.IsStatic)
                && (!requireOverride || method.IsOverride)
                && (parameterCount == AnyParameterCount || method.Parameters.Length == parameterCount))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type declares a static member of the given name and kind.</summary>
    /// <param name="type">The type whose own members are searched.</param>
    /// <param name="name">The member name.</param>
    /// <param name="kind">The kind the member must be.</param>
    /// <returns><see langword="true"/> when a matching static member is declared.</returns>
    private static bool HasStaticMember(INamedTypeSymbol type, string name, SymbolKind kind)
    {
        var members = type.GetMembers(name);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is { IsStatic: true } member && member.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }
}
