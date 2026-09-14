// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the by-name lookup of the interface member a declared member implements.</summary>
public class InterfaceImplementationLookupTests
{
    /// <summary>The class that implements every interface member implicitly.</summary>
    private const string ImplicitType = "Square";

    /// <summary>The source every lookup test binds against.</summary>
    private const string Source = """
        using System;

        interface IShape
        {
            int Area();
            event EventHandler Changed;
            int this[int index] { get; }
        }

        interface IDerivedShape : IShape
        {
            int Area();
        }

        class Square : IShape
        {
            public int Area() => 1;
            public event EventHandler Changed;
            public int this[int index] => index;
            public int Perimeter() => 4;
            int Changed2() => 0;
        }

        class Explicit : IShape
        {
            int IShape.Area() => 1;
            event EventHandler IShape.Changed { add { } remove { } }
            int IShape.this[int index] => index;
        }
        """;

    /// <summary>Verifies an implicit implementation of each member kind resolves to its interface member.</summary>
    /// <param name="memberName">The name of the member on <c>Square</c>.</param>
    /// <param name="expectedKind">The kind of the interface member found.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Area", SymbolKind.Method)]
    [Arguments("Changed", SymbolKind.Event)]
    [Arguments("this[]", SymbolKind.Property)]
    public async Task ImplicitImplementationResolvesItsInterfaceMemberAsync(string memberName, SymbolKind expectedKind)
    {
        var square = GetType(ImplicitType);
        var member = square.GetMembers(memberName)[0];

        var implemented = InterfaceImplementationLookup.FindImplementedInterfaceMember(member);

        await Assert.That(implemented).IsNotNull();
        await Assert.That(implemented!.Kind).IsEqualTo(expectedKind);
        await Assert.That(implemented.ContainingType.Name).IsEqualTo("IShape");
        await Assert.That(InterfaceImplementationLookup.ImplementsInterfaceMember(member)).IsTrue();
    }

    /// <summary>Verifies a member that implements nothing, including a same-named member of another kind, resolves to nothing.</summary>
    /// <param name="memberName">The name of the member on <c>Square</c>.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Perimeter")]
    [Arguments("Changed2")]
    public async Task UnrelatedMemberResolvesNothingAsync(string memberName)
    {
        var member = GetType(ImplicitType).GetMembers(memberName)[0];

        await Assert.That(InterfaceImplementationLookup.FindImplementedInterfaceMember(member)).IsNull();
        await Assert.That(InterfaceImplementationLookup.ImplementsInterfaceMember(member)).IsFalse();
    }

    /// <summary>Verifies an explicit implementation is not found by the by-name lookup, whose metadata name differs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExplicitImplementationIsNotMatchedByNameAsync()
    {
        var members = GetType("Explicit").GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i].IsImplicitlyDeclared)
            {
                continue;
            }

            await Assert.That(InterfaceImplementationLookup.FindImplementedInterfaceMember(members[i])).IsNull();
        }
    }

    /// <summary>Verifies a member declared in an interface is never treated as an implementation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InterfaceMemberIsNotAnImplementationAsync()
    {
        var area = GetType("IDerivedShape").GetMembers("Area")[0];

        await Assert.That(InterfaceImplementationLookup.ImplementsInterfaceMember(area)).IsFalse();
        await Assert.That(InterfaceImplementationLookup.FindImplementedInterfaceMember(area)).IsNull();
    }

    /// <summary>Verifies a symbol with no containing type resolves to nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SymbolWithoutContainingTypeResolvesNothingAsync()
    {
        var square = GetType(ImplicitType);

        await Assert.That(InterfaceImplementationLookup.FindImplementedInterfaceMember(square.ContainingNamespace)).IsNull();
        await Assert.That(InterfaceImplementationLookup.ImplementsInterfaceMember(square.ContainingNamespace)).IsFalse();
    }

    /// <summary>Binds the shared source and returns one of its declared types.</summary>
    /// <param name="name">The type name.</param>
    /// <returns>The declared type symbol.</returns>
    private static INamedTypeSymbol GetType(string name) =>
        SemanticModelFactory.Create(Source).Model.Compilation.GetTypeByMetadataName(name)!;
}
