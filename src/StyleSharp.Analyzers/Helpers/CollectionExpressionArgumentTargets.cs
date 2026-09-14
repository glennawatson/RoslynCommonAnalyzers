// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// The collection and comparer symbols SST2106 needs, resolved once per compilation so the
/// per-node path only compares already-bound symbols.
/// </summary>
internal sealed class CollectionExpressionArgumentTargets
{
    /// <summary>The collections a <c>with(...)</c> element is defined to forward arguments to.</summary>
    private readonly INamedTypeSymbol?[] _collections;

    /// <summary>The comparer interfaces that count as configuration rather than contents.</summary>
    private readonly INamedTypeSymbol?[] _comparers;

    /// <summary>Initializes a new instance of the <see cref="CollectionExpressionArgumentTargets"/> class.</summary>
    /// <param name="collections">The supported collection definitions.</param>
    /// <param name="comparers">The comparer interface definitions.</param>
    private CollectionExpressionArgumentTargets(INamedTypeSymbol?[] collections, INamedTypeSymbol?[] comparers)
    {
        _collections = collections;
        _comparers = comparers;
    }

    /// <summary>Resolves the symbols against a compilation.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns>The resolved targets, or <see langword="null"/> when no supported collection is referenced.</returns>
    internal static CollectionExpressionArgumentTargets? Resolve(Compilation compilation)
    {
        var collections = new[]
        {
            compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"),
            compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1"),
            compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2"),
        };

        var any = false;
        for (var i = 0; i < collections.Length; i++)
        {
            if (collections[i] is null)
            {
                continue;
            }

            any = true;
            break;
        }

        if (!any)
        {
            return null;
        }

        var comparers = new[]
        {
            compilation.GetTypeByMetadataName("System.Collections.Generic.IEqualityComparer`1"),
            compilation.GetTypeByMetadataName("System.Collections.Generic.IComparer`1"),
        };

        return new(collections, comparers);
    }

    /// <summary>Gets whether a created type is one of the supported collections.</summary>
    /// <param name="type">The constructed type.</param>
    /// <returns><see langword="true"/> when the type's definition is supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsSupportedCollection(INamedTypeSymbol type) => ContainsDefinition(_collections, type.OriginalDefinition);

    /// <summary>Gets whether a constructor parameter configures the collection rather than filling it.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns><see langword="true"/> for a capacity or a comparer.</returns>
    internal bool IsConfigurationParameter(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Int32)
        {
            return true;
        }

        return type is INamedTypeSymbol named && IsComparer(named);
    }

    /// <summary>Gets whether a resolved definition set holds a definition.</summary>
    /// <param name="candidates">The resolved definitions; an entry the compilation lacks is <see langword="null"/>.</param>
    /// <param name="definition">The original definition to look for.</param>
    /// <returns><see langword="true"/> when the set holds the definition.</returns>
    private static bool ContainsDefinition(INamedTypeSymbol?[] candidates, INamedTypeSymbol definition)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is not null && SymbolEqualityComparer.Default.Equals(definition, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets whether a named type is one of the comparer interfaces.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns><see langword="true"/> when the type is a comparer interface.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsComparer(INamedTypeSymbol type) => ContainsDefinition(_comparers, type.OriginalDefinition);
}
