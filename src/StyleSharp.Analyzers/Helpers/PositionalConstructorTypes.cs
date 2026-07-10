// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The BCL types whose constructors take meaning from argument position rather than from a name,
/// so a literal passed to one is already explained: <c>new DateTime(2025, 1, 1)</c>.
/// </summary>
/// <param name="DateTime">The <see cref="System.DateTime"/> symbol, when the compilation has one.</param>
/// <param name="DateTimeOffset">The <see cref="System.DateTimeOffset"/> symbol.</param>
/// <param name="TimeSpan">The <see cref="System.TimeSpan"/> symbol.</param>
/// <param name="Version">The <see cref="System.Version"/> symbol.</param>
/// <param name="Guid">The <see cref="System.Guid"/> symbol.</param>
internal readonly record struct PositionalConstructorTypes(
    INamedTypeSymbol? DateTime,
    INamedTypeSymbol? DateTimeOffset,
    INamedTypeSymbol? TimeSpan,
    INamedTypeSymbol? Version,
    INamedTypeSymbol? Guid)
{
    /// <summary>Resolves the well-known types once per compilation.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>The resolved symbols; any may be <see langword="null"/>.</returns>
    public static PositionalConstructorTypes Create(Compilation compilation) => new(
        compilation.GetTypeByMetadataName("System.DateTime"),
        compilation.GetTypeByMetadataName("System.DateTimeOffset"),
        compilation.GetTypeByMetadataName("System.TimeSpan"),
        compilation.GetTypeByMetadataName("System.Version"),
        compilation.GetTypeByMetadataName("System.Guid"));

    /// <summary>Returns whether a type is one of the positional constructor types.</summary>
    /// <param name="type">The constructed type.</param>
    /// <returns><see langword="true"/> when the type's constructor arguments are positional by convention.</returns>
    public bool Contains(INamedTypeSymbol? type) =>
        type is not null
        && (SymbolEqualityComparer.Default.Equals(type, DateTime)
            || SymbolEqualityComparer.Default.Equals(type, DateTimeOffset)
            || SymbolEqualityComparer.Default.Equals(type, TimeSpan)
            || SymbolEqualityComparer.Default.Equals(type, Version)
            || SymbolEqualityComparer.Default.Equals(type, Guid));
}
