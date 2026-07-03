// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Shared receiver-type classification for the collection rules (PSH1103, PSH1106):
/// finds a constant-time count property (<c>Count</c>/<c>Length</c>) on a receiver's
/// static type, and identifies list-like receivers whose indexer can replace LINQ
/// element access. Only properties accessible on the static type count — a member
/// exposed solely through an explicit interface implementation is ignored, because
/// the suggested rewrite would not compile at the call site.
/// </summary>
internal static class CollectionReceiverHelper
{
    /// <summary>The Count property name suggested for collection receivers.</summary>
    public const string CountPropertyName = "Count";

    /// <summary>The Length property name suggested for array and string receivers.</summary>
    public const string LengthPropertyName = "Length";

    /// <summary>Finds the constant-time count property exposed by a receiver's static type.</summary>
    /// <param name="type">The receiver's static type.</param>
    /// <param name="propertyName">The count property name when one is found.</param>
    /// <returns><see langword="true"/> when the static type exposes an accessible <see cref="int"/> Count or Length.</returns>
    public static bool TryGetCountSourceName(ITypeSymbol type, out string propertyName)
    {
        if (type is IArrayTypeSymbol || type.SpecialType == SpecialType.System_String)
        {
            propertyName = LengthPropertyName;
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (HasDirectCountProperty(current, CountPropertyName))
            {
                propertyName = CountPropertyName;
                return true;
            }

            if (HasDirectCountProperty(current, LengthPropertyName))
            {
                propertyName = LengthPropertyName;
                return true;
            }
        }

        if ((type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.TypeParameter)
            && IsOrImplementsCountInterface(type))
        {
            propertyName = CountPropertyName;
            return true;
        }

        propertyName = string.Empty;
        return false;
    }

    /// <summary>Returns whether a receiver's static type can be indexed like a list.</summary>
    /// <param name="type">The receiver's static type.</param>
    /// <returns><see langword="true"/> for rank-1 arrays, <see cref="string"/>, and types that are or implement <c>IList&lt;T&gt;</c>/<c>IReadOnlyList&lt;T&gt;</c>.</returns>
    public static bool IsListLike(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return array.Rank == 1;
        }

        if (type.SpecialType == SpecialType.System_String || IsListInterface(type))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (IsListInterface(interfaces[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type directly declares a public instance <see cref="int"/> property with the given name.</summary>
    /// <param name="type">The type whose declared members are searched.</param>
    /// <param name="name">The property name to look for.</param>
    /// <returns><see langword="true"/> when a matching property is declared on <paramref name="type"/> itself.</returns>
    private static bool HasDirectCountProperty(ITypeSymbol type, string name)
    {
        var members = type.GetMembers(name);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public, Type.SpecialType: SpecialType.System_Int32 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an interface or type parameter carries Count via <c>ICollection&lt;T&gt;</c> or <c>IReadOnlyCollection&lt;T&gt;</c>.</summary>
    /// <param name="type">The interface or type-parameter receiver type.</param>
    /// <returns><see langword="true"/> when member lookup on the static type finds the interface Count.</returns>
    private static bool IsOrImplementsCountInterface(ITypeSymbol type)
    {
        if (IsCountInterface(type))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (IsCountInterface(interfaces[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is <c>ICollection&lt;T&gt;</c> or <c>IReadOnlyCollection&lt;T&gt;</c>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for the two generic count-bearing collection interfaces.</returns>
    private static bool IsCountInterface(ITypeSymbol type)
        => type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_ICollection_T
            or SpecialType.System_Collections_Generic_IReadOnlyCollection_T;

    /// <summary>Returns whether a type is <c>IList&lt;T&gt;</c> or <c>IReadOnlyList&lt;T&gt;</c>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for the two generic indexer-bearing list interfaces.</returns>
    private static bool IsListInterface(ITypeSymbol type)
        => type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IList_T
            or SpecialType.System_Collections_Generic_IReadOnlyList_T;
}
