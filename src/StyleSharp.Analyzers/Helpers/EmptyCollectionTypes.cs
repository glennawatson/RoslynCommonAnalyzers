// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The types and members a rule needs before it can suggest writing an empty collection, resolved from
/// the compilation being analyzed rather than assumed from a target framework.
/// </summary>
/// <remarks>
/// <para>
/// A suggestion is only correct if the thing it suggests exists here: <c>Array.Empty&lt;T&gt;()</c> is
/// absent below .NET Framework 4.6, and a project can be compiled against a reference set with no
/// <c>HashSet&lt;T&gt;</c> at all. A rule that suggests one anyway hands the reader a diagnostic they
/// cannot act on, or a fix that will not build. Everything here is looked up in the compilation, and a
/// caller that gets <see langword="null"/> back must not make the suggestion.
/// </para>
/// <para>
/// Each lookup happens at most once per compilation, and none of them happen until a violation has
/// already been found — the constructor does no work, so an analysis that reports nothing pays nothing.
/// The caches race benignly: two threads that resolve the same symbol resolve it to the same value.
/// </para>
/// </remarks>
internal sealed class EmptyCollectionTypes
{
    /// <summary>The metadata name of the generic set type.</summary>
    private const string HashSetMetadataName = "System.Collections.Generic.HashSet`1";

    /// <summary>The metadata name of the generic dictionary type.</summary>
    private const string DictionaryMetadataName = "System.Collections.Generic.Dictionary`2";

    /// <summary>The name of the member that hands back the shared empty array.</summary>
    private const string EmptyArrayMemberName = "Empty";

    /// <summary>The compilation every lookup is answered from.</summary>
    private readonly Compilation _compilation;

    /// <summary>The resolved set type, once looked up.</summary>
    private INamedTypeSymbol? _hashSet;

    /// <summary>The resolved dictionary type, once looked up.</summary>
    private INamedTypeSymbol? _dictionary;

    /// <summary>Whether the set type has been looked up.</summary>
    private bool _hashSetResolved;

    /// <summary>Whether the dictionary type has been looked up.</summary>
    private bool _dictionaryResolved;

    /// <summary>Whether the shared empty array has been probed, and what the answer was.</summary>
    private bool? _arrayEmpty;

    /// <summary>Initializes a new instance of the <see cref="EmptyCollectionTypes"/> class.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    public EmptyCollectionTypes(Compilation compilation) => _compilation = compilation;

    /// <summary>Returns whether the compilation offers the shared empty array.</summary>
    /// <returns><see langword="true"/> when <c>Array.Empty&lt;T&gt;()</c> can be written.</returns>
    public bool HasEmptyArray()
    {
        _arrayEmpty ??= _compilation.GetSpecialType(SpecialType.System_Array).GetMembers(EmptyArrayMemberName).Length > 0;
        return _arrayEmpty.Value;
    }

    /// <summary>Gets how <c>System.Array</c> has to be written at a position, given what that file imports.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The position the name is written at.</param>
    /// <returns>The array type's minimal name there.</returns>
    public string GetArrayName(SemanticModel model, int position)
        => _compilation.GetSpecialType(SpecialType.System_Array).ToMinimalDisplayString(model, position);

    /// <summary>Gets the compilation's generic set type.</summary>
    /// <returns>The set type, or <see langword="null"/> when the compilation has none.</returns>
    public INamedTypeSymbol? GetHashSet()
    {
        if (!_hashSetResolved)
        {
            _hashSet = _compilation.GetTypeByMetadataName(HashSetMetadataName);
            _hashSetResolved = true;
        }

        return _hashSet;
    }

    /// <summary>Gets the compilation's generic dictionary type.</summary>
    /// <returns>The dictionary type, or <see langword="null"/> when the compilation has none.</returns>
    public INamedTypeSymbol? GetDictionary()
    {
        if (!_dictionaryResolved)
        {
            _dictionary = _compilation.GetTypeByMetadataName(DictionaryMetadataName);
            _dictionaryResolved = true;
        }

        return _dictionary;
    }
}
