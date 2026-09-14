// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared first-demand resolution of a fixed list of well-known types.</summary>
public sealed class LazyMetadataTypesUnitTest
{
    /// <summary>A compilation that sees the platform and declares nothing.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(LazyMetadataTypesUnitTest),
        references: RuntimeMetadataReferences.Platform);

    /// <summary>Verifies each name keeps its slot, a missing type leaves its slot null, and later calls return the same array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeepsSlotOrderAsync()
    {
        string[] names = ["System.String", "Missing.Type", "System.Int32"];
        var lookup = new LazyMetadataTypes(Compilation, names);

        var types = lookup.Get();

        await Assert.That(types.Length).IsEqualTo(names.Length);
        await Assert.That(types[0]?.Name).IsEqualTo("String");
        await Assert.That(types[1]).IsNull();
        await Assert.That(types[2]?.Name).IsEqualTo("Int32");
        await Assert.That(ReferenceEquals(lookup.Get(), types)).IsTrue();
    }

    /// <summary>Verifies an empty name list yields an empty result.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyListAsync() =>
        await Assert.That(new LazyMetadataTypes(Compilation, []).Get().Length).IsEqualTo(0);
}
