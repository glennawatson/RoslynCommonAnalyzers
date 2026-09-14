// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Resolves sets of well-known types by metadata name against one compilation.</summary>
internal static class MetadataTypeLookup
{
    /// <summary>Resolves every metadata name the compilation can see, dropping the ones it cannot.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    /// <returns>The resolved types in the order of <paramref name="metadataNames"/>, right-sized; empty when none resolve.</returns>
    internal static INamedTypeSymbol[] ResolveAll(Compilation compilation, string[] metadataNames)
    {
        var buffer = new INamedTypeSymbol[metadataNames.Length];
        var count = 0;
        for (var i = 0; i < metadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(metadataNames[i]) is not { } type)
            {
                continue;
            }

            buffer[count] = type;
            count++;
        }

        return ArrayBuffers.RightSize(buffer, count);
    }
}
