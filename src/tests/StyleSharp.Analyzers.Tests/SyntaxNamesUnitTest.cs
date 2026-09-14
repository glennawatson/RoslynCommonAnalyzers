// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared reading of the identifier a written name ends in.</summary>
public sealed class SyntaxNamesUnitTest
{
    /// <summary>Verifies every name shape yields its rightmost identifier, without type arguments.</summary>
    /// <param name="name">The name as written.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Foo")]
    [Arguments("Foo<int>")]
    [Arguments("A.B.Foo")]
    [Arguments("A.Foo<int>")]
    [Arguments("global::Foo")]
    public async Task GetSimpleNameOfANameIsTheRightmostIdentifierAsync(string name) =>
        await Assert.That(SyntaxNames.GetSimpleName(SyntaxFactory.ParseName(name))).IsEqualTo("Foo");

    /// <summary>Verifies a type written as a name yields its identifier and any other type yields nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetSimpleNameOfATypeNeedsANameAsync()
    {
        await Assert.That(SyntaxNames.GetSimpleName(SyntaxFactory.ParseTypeName("System.Foo"))).IsEqualTo("Foo");
        await Assert.That(SyntaxNames.GetSimpleName(SyntaxFactory.ParseTypeName("int"))).IsNull();
        await Assert.That(SyntaxNames.GetSimpleName(SyntaxFactory.ParseTypeName("Foo[]"))).IsNull();
        await Assert.That(SyntaxNames.GetSimpleName(SyntaxFactory.ParseTypeName("Foo?"))).IsNull();
    }

    /// <summary>Verifies the identifier reading rejects an unqualified generic name but reads a qualified one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetIdentifierNameRejectsOnlyAnUnqualifiedGenericAsync()
    {
        await Assert.That(SyntaxNames.GetIdentifierName(SyntaxFactory.ParseTypeName("Foo"))).IsEqualTo("Foo");
        await Assert.That(SyntaxNames.GetIdentifierName(SyntaxFactory.ParseTypeName("A.Foo"))).IsEqualTo("Foo");
        await Assert.That(SyntaxNames.GetIdentifierName(SyntaxFactory.ParseTypeName("global::Foo"))).IsEqualTo("Foo");
        await Assert.That(SyntaxNames.GetIdentifierName(SyntaxFactory.ParseTypeName("A.Foo<int>"))).IsEqualTo("Foo");
        await Assert.That(SyntaxNames.GetIdentifierName(SyntaxFactory.ParseTypeName("Foo<int>"))).IsNull();
        await Assert.That(SyntaxNames.GetIdentifierName(SyntaxFactory.ParseTypeName("int"))).IsNull();
    }

    /// <summary>Verifies a name or member access yields its rightmost identifier and any other expression yields nothing.</summary>
    /// <param name="text">The expression as written.</param>
    /// <param name="expected">The expected identifier.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Foo", "Foo")]
    [Arguments("a.b.Foo", "Foo")]
    [Arguments("global::Foo", "Foo")]
    [Arguments("Foo()", null)]
    [Arguments("1", null)]
    public async Task GetMemberNameReadsNamesAndMemberAccessesAsync(string text, string? expected) =>
        await Assert.That(SyntaxNames.GetMemberName(SyntaxFactory.ParseExpression(text))).IsEqualTo(expected);

    /// <summary>Verifies the attribute scan reads every attribute in every list by its simple name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnyAttributeNamedScansEveryListAsync()
    {
        var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("[First, Ns.Second] [Third<int>] void M() { }")!;

        await Assert.That(SyntaxNames.AnyAttributeNamed(method.AttributeLists, static name => name == "Second")).IsTrue();
        await Assert.That(SyntaxNames.AnyAttributeNamed(method.AttributeLists, static name => name == "Third")).IsTrue();
        await Assert.That(SyntaxNames.AnyAttributeNamed(method.AttributeLists, static name => name == "Fourth")).IsFalse();
        await Assert.That(SyntaxNames.AnyAttributeNamed(default, static _ => true)).IsFalse();
    }
}
