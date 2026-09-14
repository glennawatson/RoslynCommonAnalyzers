// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared structural classification of collection types.</summary>
public sealed class CollectionTypeClassificationUnitTest
{
    /// <summary>A compilation that sees the platform and declares nothing.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(CollectionTypeClassificationUnitTest),
        references: RuntimeMetadataReferences.Platform);

    /// <summary>Verifies arrays and enumerable types are collections.</summary>
    /// <param name="metadataName">The type's metadata name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("System.Collections.Generic.List`1")]
    [Arguments("System.Collections.Generic.IEnumerable`1")]
    [Arguments("System.Collections.IEnumerable")]
    [Arguments("System.Collections.Immutable.ImmutableList`1")]
    public async Task EnumerableTypesAreCollectionsAsync(string metadataName) =>
        await Assert.That(CollectionTypeClassification.IsCollection(Type(metadataName))).IsTrue();

    /// <summary>Verifies a string, a span and a scalar are not collections.</summary>
    /// <param name="metadataName">The type's metadata name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("System.String")]
    [Arguments("System.Span`1")]
    [Arguments("System.Int32")]
    public async Task StringsSpansAndScalarsAreNotCollectionsAsync(string metadataName) =>
        await Assert.That(CollectionTypeClassification.IsCollection(Type(metadataName))).IsFalse();

    /// <summary>Verifies an array is a collection and a mutable one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArraysAreMutableCollectionsAsync()
    {
        var array = Compilation.CreateArrayTypeSymbol(Compilation.GetSpecialType(SpecialType.System_Int32));

        await Assert.That(CollectionTypeClassification.IsCollection(array)).IsTrue();
        await Assert.That(CollectionTypeClassification.IsMutableCollection(array)).IsTrue();
    }

    /// <summary>Verifies types carrying a mutating collection interface are mutable.</summary>
    /// <param name="metadataName">The type's metadata name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("System.Collections.Generic.List`1")]
    [Arguments("System.Collections.Generic.Dictionary`2")]
    [Arguments("System.Collections.Concurrent.ConcurrentDictionary`2")]
    [Arguments("System.Collections.Generic.ICollection`1")]
    public async Task MutatingInterfacesMakeACollectionMutableAsync(string metadataName) =>
        await Assert.That(CollectionTypeClassification.IsMutableCollection(Type(metadataName))).IsTrue();

    /// <summary>Verifies immutable, frozen, read-only wrapper and read-only interface types are not mutable.</summary>
    /// <param name="metadataName">The type's metadata name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("System.Collections.Immutable.ImmutableList`1")]
    [Arguments("System.Collections.Frozen.FrozenSet`1")]
    [Arguments("System.Collections.ObjectModel.ReadOnlyCollection`1")]
    [Arguments("System.Collections.Generic.IEnumerable`1")]
    [Arguments("System.String")]
    public async Task FixedContentTypesAreNotMutableAsync(string metadataName) =>
        await Assert.That(CollectionTypeClassification.IsMutableCollection(Type(metadataName))).IsFalse();

    /// <summary>Verifies the framework generic collections namespace is recognized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsInSystemCollectionsGenericAsync()
    {
        await Assert.That(CollectionTypeClassification.IsInSystemCollectionsGeneric(Type("System.Collections.Generic.List`1"))).IsTrue();
        await Assert.That(CollectionTypeClassification.IsInSystemCollectionsGeneric(Type("System.Collections.ObjectModel.ReadOnlyCollection`1"))).IsFalse();
    }

    /// <summary>Resolves a platform type.</summary>
    /// <param name="metadataName">The type's metadata name.</param>
    /// <returns>The type.</returns>
    private static INamedTypeSymbol Type(string metadataName) => Compilation.GetTypeByMetadataName(metadataName)!;
}
