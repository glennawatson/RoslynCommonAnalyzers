// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared resolution of a set of well-known types.</summary>
public sealed class MetadataTypeLookupUnitTest
{
    /// <summary>Two platform types that always resolve.</summary>
    private static readonly string[] PlatformNames = ["System.String", "System.Int32"];

    /// <summary>A compilation that sees the platform and declares nothing.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(MetadataTypeLookupUnitTest),
        references: RuntimeMetadataReferences.Platform);

    /// <summary>Verifies unresolved names are dropped and the resolved ones keep their order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DropsUnresolvedNamesInOrderAsync()
    {
        var resolved = MetadataTypeLookup.ResolveAll(Compilation, [PlatformNames[0], "Missing.Type", PlatformNames[1]]);

        await Assert.That(resolved.Length).IsEqualTo(PlatformNames.Length);
        await Assert.That(resolved[0].Name).IsEqualTo("String");
        await Assert.That(resolved[1].Name).IsEqualTo("Int32");
    }

    /// <summary>Verifies every name resolving yields every type, and none resolving yields an empty set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllOrNothingAsync()
    {
        await Assert.That(MetadataTypeLookup.ResolveAll(Compilation, PlatformNames).Length).IsEqualTo(PlatformNames.Length);
        await Assert.That(MetadataTypeLookup.ResolveAll(Compilation, ["Missing.One", "Missing.Two"]).Length).IsEqualTo(0);
        await Assert.That(MetadataTypeLookup.ResolveAll(Compilation, []).Length).IsEqualTo(0);
    }
}
