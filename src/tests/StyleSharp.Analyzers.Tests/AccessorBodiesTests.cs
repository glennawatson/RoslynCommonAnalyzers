// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the detection of an implemented accessor in an accessor list.</summary>
public class AccessorBodiesTests
{
    /// <summary>Verifies a block or expression body on any accessor counts, and a bodyless list does not.</summary>
    /// <param name="member">The property declaration.</param>
    /// <param name="expected">Whether an accessor supplies an implementation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int P { get; set; }", false)]
    [Arguments("int P { get; init; }", false)]
    [Arguments("int P { get => 0; }", true)]
    [Arguments("int P { get; set { } }", true)]
    [Arguments("int P => 0;", false)]
    public async Task AnyHasBodyFindsAnImplementedAccessorAsync(string member, bool expected)
    {
        var property = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(member)!;

        await Assert.That(AccessorBodies.AnyHasBody(property.AccessorList)).IsEqualTo(expected);
    }

    /// <summary>Verifies event accessors are read the same way as property accessors.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EventAccessorsAreReadAsync()
    {
        var @event = (EventDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("event System.Action E { add { } remove { } }")!;

        await Assert.That(AccessorBodies.AnyHasBody(@event.AccessorList)).IsTrue();
        await Assert.That(AccessorBodies.AnyHasBody(null)).IsFalse();
    }
}
