// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Resolves the well-known types a compilation can see from a name list on first demand and caches them, including an empty result.</summary>
/// <param name="compilation">The compilation whose references supply the types.</param>
/// <param name="metadataNames">The metadata names to resolve.</param>
/// <param name="include">Returns whether a resolved type is kept, or <see langword="null"/> to keep every one.</param>
internal sealed class LazyMetadataTypeSet(Compilation compilation, string[] metadataNames, Func<INamedTypeSymbol, bool>? include)
{
    /// <summary>The resolved types, null until first demand.</summary>
    private INamedTypeSymbol[]? _resolved;

    /// <summary>Initializes a new instance of the <see cref="LazyMetadataTypeSet"/> class that keeps every resolved type.</summary>
    /// <param name="compilation">The compilation whose references supply the types.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    internal LazyMetadataTypeSet(Compilation compilation, string[] metadataNames)
        : this(compilation, metadataNames, null)
    {
    }

    /// <summary>Gets the types, resolving them on first demand.</summary>
    /// <returns>The kept types in name order, right-sized; empty when none resolve.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal INamedTypeSymbol[] Get() =>
        _resolved ??= include is null
            ? MetadataTypeLookup.ResolveAll(compilation, metadataNames)
            : MetadataTypeLookup.ResolveAll(compilation, metadataNames, include);
}
