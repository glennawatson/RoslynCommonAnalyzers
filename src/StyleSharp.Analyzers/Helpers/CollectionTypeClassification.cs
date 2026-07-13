// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Answers the two questions the design rules ask about a type: "is this a sequence a caller would
/// iterate?" and "can a caller add to and remove from it?". Both are answered structurally — by the
/// interfaces the type carries and the namespace it lives in — so no well-known type has to be
/// resolved, and the answer is the same on every target framework.
/// </summary>
/// <remarks>
/// <b>This is not <see cref="MutableCollectionTypes"/>, and the two must not be merged.</b> That one is a
/// deliberately narrow allow-list of the mutable shapes a static field must not expose, and it excludes the
/// concurrent collections on purpose. This one is a structural test that answers for any type — including
/// the concurrent ones, which genuinely are collections a caller can add to. Folding this generality into
/// that allow-list would make SST1499 report every correctly-shared static concurrent collection.
/// </remarks>
internal static class CollectionTypeClassification
{
    /// <summary>The <c>System</c> namespace name.</summary>
    private const string SystemNamespaceName = "System";

    /// <summary>The <c>System.Collections</c> namespace name.</summary>
    private const string CollectionsNamespaceName = "Collections";

    /// <summary>The <c>System.Collections.Generic</c> namespace name.</summary>
    private const string GenericNamespaceName = "Generic";

    /// <summary>The <c>System.Collections.Immutable</c> namespace name.</summary>
    private const string ImmutableNamespaceName = "Immutable";

    /// <summary>The <c>System.Collections.Frozen</c> namespace name.</summary>
    private const string FrozenNamespaceName = "Frozen";

    /// <summary>The <c>System.Collections.ObjectModel</c> namespace name.</summary>
    private const string ObjectModelNamespaceName = "ObjectModel";

    /// <summary>The prefix the read-only wrappers in <c>System.Collections.ObjectModel</c> share.</summary>
    private const string ReadOnlyTypeNamePrefix = "ReadOnly";

    /// <summary>Returns whether a type is a sequence a caller would iterate rather than a scalar.</summary>
    /// <param name="type">The type to classify.</param>
    /// <returns><see langword="true"/> for arrays and non-string, non-ref-like enumerables.</returns>
    /// <remarks>
    /// A string is an <c>IEnumerable&lt;char&gt;</c> the language never asks anyone to treat as a
    /// collection, and a span is a view over storage that cannot be null in the first place. Both are
    /// rejected before the interface walk.
    /// </remarks>
    public static bool IsCollection(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        if (type.SpecialType == SpecialType.System_String || type.IsRefLikeType)
        {
            return false;
        }

        if (type.OriginalDefinition.SpecialType is SpecialType.System_Collections_IEnumerable
            or SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (interfaces[i].SpecialType == SpecialType.System_Collections_IEnumerable)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type's contents can be changed through the reference a caller already holds.</summary>
    /// <param name="type">The type to classify.</param>
    /// <returns><see langword="true"/> for arrays and for types carrying a mutating collection interface.</returns>
    /// <remarks>
    /// The mutating interfaces are the test, because they are what a caller reaches for: a type that
    /// carries <c>ICollection&lt;T&gt;</c> can be added to and cleared. The immutable, frozen, and
    /// read-only-wrapper types carry those interfaces too — and throw from every one of them — so they
    /// are rejected by namespace before the interfaces are looked at.
    /// </remarks>
    public static bool IsMutableCollection(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        if (type is not INamedTypeSymbol named || named.SpecialType == SpecialType.System_String || named.IsRefLikeType)
        {
            return false;
        }

        if (IsImmutableOrReadOnlyType(named))
        {
            return false;
        }

        if (IsMutatingInterface(named))
        {
            return true;
        }

        var interfaces = named.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (IsMutatingInterface(interfaces[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is declared in <c>System.Collections.Generic</c>.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the framework, and not the caller, owns the name.</returns>
    /// <remarks>A rule that keys off a type name — <c>IList</c>, <c>ISet</c> — has to know whose name it is.</remarks>
    public static bool IsInSystemCollectionsGeneric(INamedTypeSymbol type)
        => IsCollectionsChildNamespace(type.ContainingNamespace, GenericNamespaceName);

    /// <summary>Returns whether a named type is one of the interfaces through which a collection is changed.</summary>
    /// <param name="type">The candidate interface.</param>
    /// <returns><see langword="true"/> for the add/remove interfaces in <c>System.Collections</c> and its generic twin.</returns>
    private static bool IsMutatingInterface(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Interface)
        {
            return false;
        }

        var containing = type.ContainingNamespace;
        if (!IsCollectionsNamespace(containing) && !IsCollectionsChildNamespace(containing, GenericNamespaceName))
        {
            return false;
        }

        return type.Name is "ICollection" or "IList" or "IDictionary" or "ISet";
    }

    /// <summary>Returns whether a named type is one whose collection interfaces are a facade over fixed contents.</summary>
    /// <param name="type">The candidate type.</param>
    /// <returns><see langword="true"/> for the immutable, frozen, and read-only wrapper collections.</returns>
    private static bool IsImmutableOrReadOnlyType(INamedTypeSymbol type)
    {
        var containing = type.ContainingNamespace;
        if (IsCollectionsChildNamespace(containing, ImmutableNamespaceName)
            || IsCollectionsChildNamespace(containing, FrozenNamespaceName))
        {
            return true;
        }

        return IsCollectionsChildNamespace(containing, ObjectModelNamespaceName)
            && type.Name.StartsWith(ReadOnlyTypeNamePrefix, StringComparison.Ordinal);
    }

    /// <summary>Returns whether a namespace is <c>System.Collections</c>.</summary>
    /// <param name="namespaceSymbol">The namespace to test.</param>
    /// <returns><see langword="true"/> when the namespace is exactly <c>System.Collections</c>.</returns>
    private static bool IsCollectionsNamespace(INamespaceSymbol? namespaceSymbol)
        => namespaceSymbol is
        {
            Name: CollectionsNamespaceName,
            ContainingNamespace: { Name: SystemNamespaceName, ContainingNamespace.IsGlobalNamespace: true },
        };

    /// <summary>Returns whether a namespace is the named child of <c>System.Collections</c>.</summary>
    /// <param name="namespaceSymbol">The namespace to test.</param>
    /// <param name="name">The expected child namespace name.</param>
    /// <returns><see langword="true"/> when the namespace is <c>System.Collections.&lt;name&gt;</c>.</returns>
    private static bool IsCollectionsChildNamespace(INamespaceSymbol? namespaceSymbol, string name)
        => namespaceSymbol is not null
            && string.Equals(namespaceSymbol.Name, name, StringComparison.Ordinal)
            && IsCollectionsNamespace(namespaceSymbol.ContainingNamespace);
}
