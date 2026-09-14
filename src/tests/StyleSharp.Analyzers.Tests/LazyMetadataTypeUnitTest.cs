// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared first-demand resolution of one well-known type.</summary>
public sealed class LazyMetadataTypeUnitTest
{
    /// <summary>A compilation that sees the platform and declares nothing.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(LazyMetadataTypeUnitTest),
        references: RuntimeMetadataReferences.Platform);

    /// <summary>Verifies a visible type resolves and later calls return the same symbol.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResolvesVisibleTypeAsync()
    {
        var lookup = new LazyMetadataType(Compilation, "System.String");

        var first = lookup.Get();

        await Assert.That(first?.Name).IsEqualTo("String");
        await Assert.That(ReferenceEquals(lookup.Get(), first)).IsTrue();
    }

    /// <summary>Verifies a type the compilation cannot see stays null on every call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingTypeIsNullAsync()
    {
        var lookup = new LazyMetadataType(Compilation, "Missing.Type");

        await Assert.That(lookup.Get()).IsNull();
        await Assert.That(lookup.Get()).IsNull();
    }
}
