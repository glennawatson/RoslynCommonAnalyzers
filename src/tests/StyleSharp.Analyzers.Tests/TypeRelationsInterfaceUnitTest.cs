// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the interface-definition and interface-member relations.</summary>
public sealed class TypeRelationsInterfaceUnitTest
{
    /// <summary>Source with a generic interface, an implicit and an explicit implementation, and an unrelated member.</summary>
    private const string Source =
        """
        interface IStore<T> { void Put(T item); }
        interface IClosable { void Close(); }
        class Store : IStore<int>, IClosable
        {
            public void Put(int item) { }
            void IClosable.Close() { }
            public void Other() { }
        }
        class Plain { }
        """;

    /// <summary>The compilation the tests bind against.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(TypeRelationsInterfaceUnitTest),
        [CSharpSyntaxTree.ParseText(Source)],
        [RuntimeMetadataReferences.CoreLibrary]);

    /// <summary>Verifies a constructed implementation, and the definition itself, match the unbound definition.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsOrImplementsDefinitionMatchesConstructedFormsAsync()
    {
        var definition = Compilation.GetTypeByMetadataName("IStore`1")!;
        var store = Compilation.GetTypeByMetadataName("Store")!;
        var plain = Compilation.GetTypeByMetadataName("Plain")!;

        await Assert.That(TypeRelations.IsOrImplementsDefinition(store, definition)).IsTrue();
        await Assert.That(TypeRelations.IsOrImplementsDefinition(definition, definition)).IsTrue();
        await Assert.That(TypeRelations.IsOrImplementsDefinition(plain, definition)).IsFalse();
    }

    /// <summary>Verifies implicit and explicit implementations are found and an unrelated member is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplementsAnyInterfaceMemberFindsImplicitAndExplicitImplementationsAsync()
    {
        var store = Compilation.GetTypeByMetadataName("Store")!;
        var members = store.GetMembers();

        await Assert.That(TypeRelations.ImplementsAnyInterfaceMember(store, members.Single(static m => m.Name == "Put"))).IsTrue();
        await Assert.That(TypeRelations.ImplementsAnyInterfaceMember(store, members.Single(static m => m.Name == "IClosable.Close"))).IsTrue();
        await Assert.That(TypeRelations.ImplementsAnyInterfaceMember(store, members.Single(static m => m.Name == "Other"))).IsFalse();
    }
}
