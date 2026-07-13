// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// The collection types whose contents any holder of the reference can change, resolved once per
/// compilation and only when a rule first needs them.
/// </summary>
/// <remarks>
/// <para>
/// The set is a deliberate allow-list of the mutable shapes, not a deny-list of the immutable ones: a type
/// this set does not name is left alone. An analyzer cannot prove a hand-written type immutable, and a rule
/// that guesses would report every singleton in the codebase. The immutable collections, the frozen ones,
/// the read-only wrappers and every user type therefore pass without a word.
/// </para>
/// <para>
/// The concurrent collections are absent on purpose. They are mutable, but they exist to be shared and
/// mutated by many threads at once; a static one is the pattern working, not the pattern failing.
/// </para>
/// <para>
/// The lookup is lazy because a compilation that never declares a visible static field must not pay to
/// resolve nineteen metadata names.
/// </para>
/// <para>
/// <b>This is not <see cref="CollectionTypeClassification"/>, and the two must not be merged.</b> That one
/// asks a structural question — does this type carry the collection interfaces — and answers it for any
/// type on any framework. This one asks a narrower and more opinionated question: is the type one of the
/// specific mutable shapes whose contents a caller can quietly change behind a static field. The difference
/// is load bearing at exactly one point: a structural test says a concurrent collection is mutable, which
/// is true and unhelpful, and merging the two would make this rule report every correctly-shared static
/// <c>ConcurrentDictionary</c> in the codebase.
/// </para>
/// </remarks>
internal sealed class MutableCollectionTypes
{
    /// <summary>The metadata names of the collection types whose contents a caller can change.</summary>
    private static readonly string[] MetadataNames =
    [
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.Queue`1",
        "System.Collections.Generic.Stack`1",
        "System.Collections.Generic.LinkedList`1",
        "System.Collections.Generic.ICollection`1",
        "System.Collections.Generic.IList`1",
        "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.ISet`1",
        "System.Collections.ObjectModel.Collection`1",
        "System.Collections.ObjectModel.KeyedCollection`2",
        "System.Collections.ObjectModel.ObservableCollection`1",
        "System.Collections.ArrayList",
        "System.Collections.Hashtable",
        "System.Collections.ICollection",
        "System.Collections.IDictionary",
        "System.Collections.IList",
    ];

    /// <summary>The resolved type symbols, built on first use.</summary>
    private readonly Lazy<HashSet<ISymbol>> _types;

    /// <summary>Initializes a new instance of the <see cref="MutableCollectionTypes"/> class.</summary>
    /// <param name="compilation">The compilation whose types are resolved.</param>
    public MutableCollectionTypes(Compilation compilation)
        => _types = new Lazy<HashSet<ISymbol>>(() => Resolve(compilation));

    /// <summary>Returns whether a field's type is one whose contents a caller can change.</summary>
    /// <param name="type">The field's type.</param>
    /// <returns><see langword="true"/> for an array, and for a collection this set names.</returns>
    public bool IsMutable(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Array)
        {
            return true;
        }

        return type is INamedTypeSymbol named && _types.Value.Contains(named.OriginalDefinition);
    }

    /// <summary>Resolves the named types that exist in the compilation.</summary>
    /// <param name="compilation">The compilation whose types are resolved.</param>
    /// <returns>The resolved type symbols.</returns>
    private static HashSet<ISymbol> Resolve(Compilation compilation)
    {
        var types = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var i = 0; i < MetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(MetadataNames[i]) is { } type)
            {
                types.Add(type);
            }
        }

        return types;
    }
}
