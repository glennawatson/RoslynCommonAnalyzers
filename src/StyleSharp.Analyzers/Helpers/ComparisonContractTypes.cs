// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The comparison and equality contracts of one compilation, resolved once: the generic contracts a type may
/// sign (<c>IComparable&lt;T&gt;</c>, <c>IComparer&lt;T&gt;</c>, <c>IEqualityComparer&lt;T&gt;</c>,
/// <c>IEquatable&lt;T&gt;</c>) and the non-generic counterparts the runtime still reaches for
/// (<c>IComparable</c>, <c>IComparer</c>, <c>IEqualityComparer</c>). A framework that has none of the generic
/// contracts yields nothing, and the rule never registers.
/// </summary>
/// <param name="ComparableOfT">The generic <c>System.IComparable&lt;T&gt;</c> definition, when present.</param>
/// <param name="ComparerOfT">The generic <c>System.Collections.Generic.IComparer&lt;T&gt;</c> definition, when present.</param>
/// <param name="EqualityComparerOfT">The generic <c>System.Collections.Generic.IEqualityComparer&lt;T&gt;</c> definition, when present.</param>
/// <param name="EquatableOfT">The generic <c>System.IEquatable&lt;T&gt;</c> definition, when present.</param>
/// <param name="Comparable">The non-generic <c>System.IComparable</c>, when present.</param>
/// <param name="Comparer">The non-generic <c>System.Collections.IComparer</c>, when present.</param>
/// <param name="EqualityComparer">The non-generic <c>System.Collections.IEqualityComparer</c>, when present.</param>
internal readonly record struct ComparisonContractTypes(
    INamedTypeSymbol? ComparableOfT,
    INamedTypeSymbol? ComparerOfT,
    INamedTypeSymbol? EqualityComparerOfT,
    INamedTypeSymbol? EquatableOfT,
    INamedTypeSymbol? Comparable,
    INamedTypeSymbol? Comparer,
    INamedTypeSymbol? EqualityComparer)
{
    /// <summary>Resolves the comparison contracts for a compilation.</summary>
    /// <param name="compilation">The compilation to resolve against.</param>
    /// <returns>The resolved contracts, or <see langword="null"/> when no generic contract exists at all.</returns>
    public static ComparisonContractTypes? Create(Compilation compilation)
    {
        var comparableOfT = compilation.GetTypeByMetadataName("System.IComparable`1");
        var comparerOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IComparer`1");
        var equalityComparerOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IEqualityComparer`1");
        var equatableOfT = compilation.GetTypeByMetadataName("System.IEquatable`1");
        if (comparableOfT is null && comparerOfT is null && equalityComparerOfT is null && equatableOfT is null)
        {
            return null;
        }

        return new ComparisonContractTypes(
            comparableOfT,
            comparerOfT,
            equalityComparerOfT,
            equatableOfT,
            compilation.GetTypeByMetadataName("System.IComparable"),
            compilation.GetTypeByMetadataName("System.Collections.IComparer"),
            compilation.GetTypeByMetadataName("System.Collections.IEqualityComparer"));
    }

    /// <summary>Finds the type argument a type binds a generic contract with, when it implements the contract.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="contract">The unbound generic contract, or <see langword="null"/> when the framework has none.</param>
    /// <returns>The bound type argument, or <see langword="null"/> when the type does not implement the contract.</returns>
    public static ITypeSymbol? GetImplementedArgument(INamedTypeSymbol type, INamedTypeSymbol? contract)
    {
        if (contract is null)
        {
            return null;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidate = interfaces[i];
            if (candidate.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, contract))
            {
                return candidate.TypeArguments[0];
            }
        }

        return null;
    }

    /// <summary>Returns whether a type implements a non-generic interface.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="contract">The non-generic interface, or <see langword="null"/> when the framework has none.</param>
    /// <returns><see langword="true"/> when the type implements the interface.</returns>
    public static bool Implements(INamedTypeSymbol type, INamedTypeSymbol? contract)
    {
        if (contract is null)
        {
            return false;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], contract))
            {
                return true;
            }
        }

        return false;
    }
}
