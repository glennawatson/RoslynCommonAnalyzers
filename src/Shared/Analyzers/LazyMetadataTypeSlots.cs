// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Resolves well-known top-level types by metadata name one slot at a time, at most once per slot, caching absent ones.</summary>
/// <param name="compilation">The compilation whose references supply the types.</param>
/// <param name="metadataNames">The top-level, non-generic metadata names, one per slot; at most 32.</param>
internal sealed class LazyMetadataTypeSlots(Compilation compilation, string[] metadataNames)
{
    /// <summary>Serializes the first lookup of each slot.</summary>
    private readonly object _gate = new();

    /// <summary>The resolved slots, allocated on the first lookup.</summary>
    private INamedTypeSymbol?[]? _types;

    /// <summary>One published bit per resolved slot, including slots whose type is absent.</summary>
    private int _resolved;

    /// <summary>Gets the type in one slot, resolving only that slot on first demand.</summary>
    /// <param name="index">The slot.</param>
    /// <returns>The type, or <see langword="null"/> when the compilation cannot see it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal INamedTypeSymbol? Get(int index) =>
        (Volatile.Read(ref _resolved) & (1 << index)) != 0 ? _types![index] : Resolve(index);

    /// <summary>Returns whether a type is the resolved type of one of a range of slots.</summary>
    /// <param name="type">The candidate type definition.</param>
    /// <param name="start">The first slot, inclusive.</param>
    /// <param name="end">The last slot, exclusive.</param>
    /// <returns><see langword="true"/> when the type is one of the slots' types.</returns>
    /// <remarks>A slot is resolved only when the candidate carries its name and namespace.</remarks>
    internal bool IsAny(INamedTypeSymbol type, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (CouldBe(type, metadataNames[i]) && SymbolEqualityComparer.Default.Equals(type, Get(i)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type or one of its base types is the resolved type of one of a range of slots.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="start">The first slot, inclusive.</param>
    /// <param name="end">The last slot, exclusive.</param>
    /// <returns><see langword="true"/> when the type derives from, or is, one of the slots' types.</returns>
    internal bool IsOrDerivesFromAny(INamedTypeSymbol type, int start, int end)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (IsAny(current.OriginalDefinition, start, end))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type's name and namespace spell a top-level metadata name, without building a string.</summary>
    /// <param name="type">The candidate type definition.</param>
    /// <param name="metadataName">The top-level, non-generic metadata name.</param>
    /// <returns><see langword="true"/> when the type can be the named type.</returns>
    private static bool CouldBe(INamedTypeSymbol type, string metadataName)
    {
        var separator = metadataName.LastIndexOf('.');
        var name = type.Name;
        if (type.ContainingType is not null || type.Arity != 0
            || name.Length != metadataName.Length - separator - 1
            || string.Compare(metadataName, separator + 1, name, 0, name.Length, StringComparison.Ordinal) != 0)
        {
            return false;
        }

        var ns = type.ContainingNamespace;
        while (separator > 0 && !ns.IsGlobalNamespace)
        {
            var end = separator;
            separator = metadataName.LastIndexOf('.', end - 1);
            var part = ns.Name;
            if (part.Length != end - separator - 1
                || string.Compare(metadataName, separator + 1, part, 0, part.Length, StringComparison.Ordinal) != 0)
            {
                return false;
            }

            ns = ns.ContainingNamespace;
        }

        return separator < 0 && ns.IsGlobalNamespace;
    }

    /// <summary>Resolves and publishes one slot on first demand.</summary>
    /// <param name="index">The slot.</param>
    /// <returns>The type, or <see langword="null"/> when the compilation cannot see it.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private INamedTypeSymbol? Resolve(int index)
    {
        lock (_gate)
        {
            var types = _types ??= new INamedTypeSymbol?[metadataNames.Length];
            var bit = 1 << index;
            if ((_resolved & bit) == 0)
            {
                types[index] = compilation.GetTypeByMetadataName(metadataNames[index]);
                Volatile.Write(ref _resolved, _resolved | bit);
            }

            return types[index];
        }
    }
}
