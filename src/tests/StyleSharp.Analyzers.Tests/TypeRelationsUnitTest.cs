// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared base-chain and interface relations between bound types.</summary>
public sealed class TypeRelationsUnitTest
{
    /// <summary>The source whose types the tests relate.</summary>
    private const string Source =
        """
        using System;

        interface IShape { void Draw(); }
        interface ICircle : IShape { }
        class Base { }
        class Derived : Base, ICircle
        {
            public void Draw() { }
            public void Other() { }
        }
        class Explicit : IShape { void IShape.Draw() { } }
        sealed class MarkerAttribute : Attribute { public MarkerAttribute(int value) { } }
        """;

    /// <summary>The compilation every test binds against.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(TypeRelationsUnitTest),
        [CSharpSyntaxTree.ParseText(Source)],
        RuntimeMetadataReferences.Platform);

    /// <summary>The root interface.</summary>
    private static readonly INamedTypeSymbol Shape = Type("IShape");

    /// <summary>The interface deriving from <see cref="Shape"/>.</summary>
    private static readonly INamedTypeSymbol Circle = Type("ICircle");

    /// <summary>The base class.</summary>
    private static readonly INamedTypeSymbol BaseClass = Type("Base");

    /// <summary>The class deriving from <see cref="BaseClass"/> and implementing <see cref="Circle"/>.</summary>
    private static readonly INamedTypeSymbol DerivedClass = Type("Derived");

    /// <summary>The class implementing <see cref="Shape"/> explicitly.</summary>
    private static readonly INamedTypeSymbol ExplicitClass = Type("Explicit");

    /// <summary>The attribute class.</summary>
    private static readonly INamedTypeSymbol Marker = Type("MarkerAttribute");

    /// <summary>Verifies the base chain includes the type itself and its bases, but not interfaces or derived types.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsOrDerivesFromWalksOnlyTheBaseChainAsync()
    {
        await Assert.That(TypeRelations.IsOrDerivesFrom(DerivedClass, BaseClass)).IsTrue();
        await Assert.That(TypeRelations.IsOrDerivesFrom(DerivedClass, DerivedClass)).IsTrue();
        await Assert.That(TypeRelations.IsOrDerivesFrom(BaseClass, DerivedClass)).IsFalse();
        await Assert.That(TypeRelations.IsOrDerivesFrom(DerivedClass, Shape)).IsFalse();
        await Assert.That(TypeRelations.IsOrDerivesFrom(null, BaseClass)).IsFalse();
    }

    /// <summary>Verifies the any-of base chain check matches one candidate and rejects an empty set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsOrDerivesFromAnyMatchesOneCandidateAsync()
    {
        INamedTypeSymbol[] candidates = [Shape, BaseClass];
        INamedTypeSymbol[] derivedOnly = [DerivedClass];

        var derivedMatches = TypeRelations.IsOrDerivesFromAny(DerivedClass, candidates);
        var baseMatchesDerived = TypeRelations.IsOrDerivesFromAny(BaseClass, derivedOnly);
        var emptyMatches = TypeRelations.IsOrDerivesFromAny(DerivedClass, []);

        await Assert.That(derivedMatches).IsTrue();
        await Assert.That(baseMatchesDerived).IsFalse();
        await Assert.That(emptyMatches).IsFalse();
    }

    /// <summary>Verifies an interface counts as implemented transitively, and as itself only for the is-or form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplementsAndIsOrImplementsAsync()
    {
        await Assert.That(TypeRelations.Implements(DerivedClass, Shape)).IsTrue();
        await Assert.That(TypeRelations.Implements(Circle, Shape)).IsTrue();
        await Assert.That(TypeRelations.Implements(Circle, Circle)).IsFalse();
        await Assert.That(TypeRelations.Implements(BaseClass, Shape)).IsFalse();
        await Assert.That(TypeRelations.IsOrImplements(Circle, Circle)).IsTrue();
        await Assert.That(TypeRelations.IsOrImplements(DerivedClass, Circle)).IsTrue();
        await Assert.That(TypeRelations.IsOrImplements(BaseClass, Circle)).IsFalse();
    }

    /// <summary>Verifies the identity lookup matches an exact candidate only.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsOneOfMatchesIdentityOnlyAsync()
    {
        INamedTypeSymbol[] candidates = [BaseClass, Circle];

        var exact = TypeRelations.IsOneOf(BaseClass, candidates);
        var derived = TypeRelations.IsOneOf(DerivedClass, candidates);
        var missing = TypeRelations.IsOneOf(null, candidates);

        await Assert.That(exact).IsTrue();
        await Assert.That(derived).IsFalse();
        await Assert.That(missing).IsFalse();
    }

    /// <summary>Verifies attribute classes are recognized through their base chain.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsAttributeTypeAsync()
    {
        await Assert.That(TypeRelations.IsAttributeType(Marker)).IsTrue();
        await Assert.That(TypeRelations.IsAttributeType(DerivedClass)).IsFalse();
    }

    /// <summary>Verifies an implicit implementation is found, and an ordinary method or an explicit implementation is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplementsInterfaceMemberMatchesImplicitImplementationsAsync()
    {
        await Assert.That(TypeRelations.ImplementsInterfaceMember(Method(DerivedClass, "Draw"), DerivedClass)).IsTrue();
        await Assert.That(TypeRelations.ImplementsInterfaceMember(Method(DerivedClass, "Other"), DerivedClass)).IsFalse();
        await Assert.That(TypeRelations.ImplementsInterfaceMember(Method(ExplicitClass, "IShape.Draw"), ExplicitClass)).IsFalse();
    }

    /// <summary>Verifies a signature is bound by an implemented interface or by being an attribute's constructor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsSignatureBoundByContractAsync()
    {
        await Assert.That(TypeRelations.IsSignatureBoundByContract(Method(DerivedClass, "Draw"))).IsTrue();
        await Assert.That(TypeRelations.IsSignatureBoundByContract(Marker.InstanceConstructors[0])).IsTrue();
        await Assert.That(TypeRelations.IsSignatureBoundByContract(Method(DerivedClass, "Other"))).IsFalse();
    }

    /// <summary>Resolves a type declared in the test source.</summary>
    /// <param name="metadataName">The type's metadata name.</param>
    /// <returns>The declared type.</returns>
    private static INamedTypeSymbol Type(string metadataName) => Compilation.GetTypeByMetadataName(metadataName)!;

    /// <summary>Resolves a method declared on a type.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The method's member name.</param>
    /// <returns>The method.</returns>
    private static IMethodSymbol Method(INamedTypeSymbol type, string name) => (IMethodSymbol)type.GetMembers(name)[0];
}
