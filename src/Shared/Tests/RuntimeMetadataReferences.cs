// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace RoslynCommon.Analyzers.Tests;

/// <summary>
/// The metadata references for the assemblies the running test host trusts, for helper-level tests that
/// compile a snippet directly instead of going through an analyzer verifier.
/// </summary>
/// <remarks>
/// <see cref="MetadataReference.CreateFromFile(string, MetadataReferenceProperties, DocumentationProvider)"/>
/// memory-maps the assembly and holds that mapping for as long as the reference is alive, and it does no
/// caching of its own. Building the set inside a test therefore maps the entire platform again per call and
/// keeps every copy alive, which costs both the mapping work and the memory it pins. A metadata reference is
/// immutable and Roslyn shares one freely across compilations, so the set is built once and reused.
/// </remarks>
internal static class RuntimeMetadataReferences
{
    /// <summary>Gets the metadata references for the current runtime's trusted platform assemblies.</summary>
    public static ImmutableArray<MetadataReference> Platform { get; } = CreatePlatformReferences();

    /// <summary>Gets the metadata reference for the assembly that carries <see cref="object"/>.</summary>
    /// <remarks>Enough for a compilation that only has to bind the primitives, and cheaper than <see cref="Platform"/>.</remarks>
    public static MetadataReference CoreLibrary { get; } = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

    /// <summary>Maps every trusted platform assembly of the running test host.</summary>
    /// <returns>The metadata references for the current runtime.</returns>
    private static ImmutableArray<MetadataReference> CreatePlatformReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = ImmutableArray.CreateBuilder<MetadataReference>(trustedAssemblies.Length);
        for (var i = 0; i < trustedAssemblies.Length; i++)
        {
            references.Add(MetadataReference.CreateFromFile(trustedAssemblies[i]));
        }

        return references.MoveToImmutable();
    }
}
