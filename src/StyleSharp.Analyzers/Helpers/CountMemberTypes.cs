// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The well-known types whose cardinality members SST1479 trusts to be non-negative and that cannot be
/// recognized from a <see cref="SpecialType"/> alone.
/// </summary>
/// <param name="Span">The <c>System.Span&lt;T&gt;</c> definition, when the compilation has one.</param>
/// <param name="ReadOnlySpan">The <c>System.ReadOnlySpan&lt;T&gt;</c> definition.</param>
/// <param name="Enumerable">The <c>System.Linq.Enumerable</c> symbol that declares <c>Count</c>.</param>
/// <remarks>
/// Arrays, strings and the BCL collection interfaces are all identified by their <see cref="SpecialType"/>,
/// which costs nothing, so only these three need a metadata-name lookup. They are resolved behind a
/// <see cref="Lazy{T}"/> so a compilation whose comparisons never reach the bind never pays for them.
/// </remarks>
internal readonly record struct CountMemberTypes(
    INamedTypeSymbol? Span,
    INamedTypeSymbol? ReadOnlySpan,
    INamedTypeSymbol? Enumerable)
{
    /// <summary>Resolves the well-known types once per compilation.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>The resolved symbols; any may be <see langword="null"/>.</returns>
    public static CountMemberTypes Create(Compilation compilation) => new(
        compilation.GetTypeByMetadataName("System.Span`1"),
        compilation.GetTypeByMetadataName("System.ReadOnlySpan`1"),
        compilation.GetTypeByMetadataName("System.Linq.Enumerable"));

    /// <summary>Returns whether a type is <c>Span&lt;T&gt;</c> or <c>ReadOnlySpan&lt;T&gt;</c>.</summary>
    /// <param name="type">The type that declares the member.</param>
    /// <returns><see langword="true"/> when the type's <c>Length</c> counts elements of a span.</returns>
    public bool IsSpan(INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, Span)
            || SymbolEqualityComparer.Default.Equals(definition, ReadOnlySpan);
    }

    /// <summary>Returns whether a type is <c>System.Linq.Enumerable</c>.</summary>
    /// <param name="type">The type that declares the member.</param>
    /// <returns><see langword="true"/> when the type declares the LINQ counting operators.</returns>
    public bool IsEnumerable(INamedTypeSymbol type)
        => SymbolEqualityComparer.Default.Equals(type, Enumerable);
}
