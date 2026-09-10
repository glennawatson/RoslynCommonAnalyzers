// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    public static CollectionExpressionArgumentTargets? Resolve(Compilation compilation)
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
            if (collections[i] is not null)
            {
                any = true;
                break;
            }
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

        return new CollectionExpressionArgumentTargets(collections, comparers);
    }

    /// <summary>Gets whether a created type is one of the supported collections.</summary>
    /// <param name="type">The constructed type.</param>
    /// <returns><see langword="true"/> when the type's definition is supported.</returns>
    public bool IsSupportedCollection(INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition;
        foreach (var candidate in _collections)
        {
            if (candidate is not null && SymbolEqualityComparer.Default.Equals(definition, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets whether a constructor parameter configures the collection rather than filling it.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns><see langword="true"/> for a capacity or a comparer.</returns>
    public bool IsConfigurationParameter(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Int32)
        {
            return true;
        }

        return type is INamedTypeSymbol named && IsComparer(named);
    }

    /// <summary>Gets whether a named type is one of the comparer interfaces.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns><see langword="true"/> when the type is a comparer interface.</returns>
    private bool IsComparer(INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition;
        foreach (var comparer in _comparers)
        {
            if (comparer is not null && SymbolEqualityComparer.Default.Equals(definition, comparer))
            {
                return true;
            }
        }

        return false;
    }
}
