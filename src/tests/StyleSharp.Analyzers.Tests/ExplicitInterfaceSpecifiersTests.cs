// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the syntactic detection of an explicit interface implementation.</summary>
public class ExplicitInterfaceSpecifiersTests
{
    /// <summary>Verifies each member kind that can name an interface is read, and other declarations are not.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="expected">Whether the member carries an explicit interface specifier.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void IShape.Draw() { }", true)]
    [Arguments("int IShape.Area => 0;", true)]
    [Arguments("int IShape.this[int index] => index;", true)]
    [Arguments("event System.Action IShape.Changed { add { } remove { } }", true)]
    [Arguments("public void Draw() { }", false)]
    [Arguments("public int Area => 0;", false)]
    [Arguments("public int this[int index] => index;", false)]
    [Arguments("public event System.Action Changed { add { } remove { } }", false)]
    [Arguments("public event System.Action Changed;", false)]
    [Arguments("int value;", false)]
    public async Task ReadsTheSpecifierOfEachMemberKindAsync(string member, bool expected)
    {
        var declaration = SyntaxFactory.ParseMemberDeclaration(member)!;

        await Assert.That(ExplicitInterfaceSpecifiers.IsPresent(declaration)).IsEqualTo(expected);
    }
}
