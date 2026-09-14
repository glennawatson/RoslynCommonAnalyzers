// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Resolves a fixed list of well-known types by metadata name on first demand and caches them, including absent ones.</summary>
/// <param name="compilation">The compilation whose references supply the types.</param>
/// <param name="metadataNames">The metadata names, in the slot order <see cref="Get"/> returns.</param>
internal sealed class LazyMetadataTypes(Compilation compilation, string[] metadataNames)
{
    /// <summary>The resolved types by slot, null until first demand.</summary>
    private INamedTypeSymbol?[]? _resolved;

    /// <summary>Gets the types, resolving them on first demand.</summary>
    /// <returns>One slot per metadata name, holding the type or null when the compilation cannot see it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal INamedTypeSymbol?[] Get() => _resolved ??= MetadataTypeLookup.ResolveEach(compilation, metadataNames);
}
