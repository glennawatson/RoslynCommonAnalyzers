// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Resolves one well-known type by metadata name on first demand and caches the result, including its absence.</summary>
/// <param name="compilation">The compilation whose references supply the type.</param>
/// <param name="metadataName">The metadata name of the type.</param>
internal sealed class LazyMetadataType(Compilation compilation, string metadataName)
{
    /// <summary>A single slot holding the resolved type, or null when it is absent; the slot array is null until first demand.</summary>
    private INamedTypeSymbol?[]? _resolved;

    /// <summary>Gets the type, resolving it on first demand.</summary>
    /// <returns>The type, or <see langword="null"/> when the compilation cannot see it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(metadataName)])[0];
}
