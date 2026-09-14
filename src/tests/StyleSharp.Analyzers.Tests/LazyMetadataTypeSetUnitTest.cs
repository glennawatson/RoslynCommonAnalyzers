// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared first-demand resolution of the well-known types a compilation can see.</summary>
public sealed class LazyMetadataTypeSetUnitTest
{
    /// <summary>The simple name of the first platform type.</summary>
    private const string StringName = "String";

    /// <summary>Two platform types that always resolve.</summary>
    private static readonly string[] PlatformNames = ["System.String", "System.Int32"];

    /// <summary>A compilation that sees the platform and declares nothing.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(LazyMetadataTypeSetUnitTest),
        references: RuntimeMetadataReferences.Platform);

    /// <summary>Verifies missing names are dropped and later calls return the same array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DropsMissingNamesAndCachesAsync()
    {
        var lookup = new LazyMetadataTypeSet(Compilation, [PlatformNames[0], "Missing.Type", PlatformNames[1]]);

        var types = lookup.Get();

        await Assert.That(types.Length).IsEqualTo(PlatformNames.Length);
        await Assert.That(types[0].Name).IsEqualTo(StringName);
        await Assert.That(types[1].Name).IsEqualTo("Int32");
        await Assert.That(ReferenceEquals(lookup.Get(), types)).IsTrue();
    }

    /// <summary>Verifies the filter keeps only the accepted types.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FilterKeepsAcceptedTypesAsync()
    {
        var types = new LazyMetadataTypeSet(Compilation, PlatformNames, static type => type.Name == StringName).Get();

        await Assert.That(types.Length).IsEqualTo(1);
        await Assert.That(types[0].Name).IsEqualTo(StringName);
    }

    /// <summary>Verifies no resolvable name yields an empty result.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NothingResolvesAsync() =>
        await Assert.That(new LazyMetadataTypeSet(Compilation, ["Missing.One", "Missing.Two"]).Get().Length).IsEqualTo(0);
}
