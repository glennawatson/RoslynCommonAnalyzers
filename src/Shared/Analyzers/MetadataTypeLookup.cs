// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Resolves sets of well-known types by metadata name against one compilation.</summary>
internal static class MetadataTypeLookup
{
    /// <summary>Resolves every metadata name the compilation can see, dropping the ones it cannot.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    /// <returns>The resolved types in the order of <paramref name="metadataNames"/>, right-sized; empty when none resolve.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static INamedTypeSymbol[] ResolveAll(Compilation compilation, string[] metadataNames) =>
        Resolve(compilation, metadataNames, null);

    /// <summary>Resolves every metadata name the compilation can see whose type passes a filter.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    /// <param name="include">Returns whether a resolved type is kept.</param>
    /// <returns>The kept types in the order of <paramref name="metadataNames"/>, right-sized; empty when none are kept.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static INamedTypeSymbol[] ResolveAll(Compilation compilation, string[] metadataNames, Func<INamedTypeSymbol, bool> include) =>
        Resolve(compilation, metadataNames, include);

    /// <summary>Resolves each metadata name into its own slot.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    /// <returns>One slot per metadata name, holding the type or null when the compilation cannot see it.</returns>
    internal static INamedTypeSymbol?[] ResolveEach(Compilation compilation, string[] metadataNames)
    {
        var types = new INamedTypeSymbol?[metadataNames.Length];
        for (var i = 0; i < types.Length; i++)
        {
            types[i] = compilation.GetTypeByMetadataName(metadataNames[i]);
        }

        return types;
    }

    /// <summary>Returns whether the compilation can see any of the metadata names.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to probe.</param>
    /// <returns><see langword="true"/> when at least one name resolves.</returns>
    internal static bool AnyResolves(Compilation compilation, string[] metadataNames)
    {
        for (var i = 0; i < metadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(metadataNames[i]) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the metadata names that bind and pass the optional filter, allocating only once one does.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    /// <param name="include">Returns whether a resolved type is kept, or <see langword="null"/> to keep every one.</param>
    /// <returns>The kept types, right-sized; empty when none are kept.</returns>
    private static INamedTypeSymbol[] Resolve(Compilation compilation, string[] metadataNames, Func<INamedTypeSymbol, bool>? include)
    {
        INamedTypeSymbol[]? buffer = null;
        var count = 0;
        for (var i = 0; i < metadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(metadataNames[i]) is not { } type || (include is not null && !include(type)))
            {
                continue;
            }

            buffer ??= new INamedTypeSymbol[metadataNames.Length];
            buffer[count] = type;
            count++;
        }

        return buffer is null ? [] : ArrayBuffers.RightSize(buffer, count);
    }
}
