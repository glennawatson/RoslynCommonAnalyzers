// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the recognition of syntax that only compiles inside an unsafe context.</summary>
public class UnsafeContextSyntaxTests
{
    /// <summary>Verifies each unsafe-only form is found beneath a declaration, and safe code is not.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="expected">Whether the declaration needs an unsafe context.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("unsafe void M() { var x = 1; }", false)]
    [Arguments("unsafe void M(int* p) { }", true)]
    [Arguments("unsafe void M(delegate*<void> p) { }", true)]
    [Arguments("unsafe void M(string s) { fixed (char* p = s) { } }", true)]
    [Arguments("unsafe int M() => sizeof(int);", true)]
    [Arguments("unsafe void M(int x) { var p = &x; }", true)]
    [Arguments("void M() { unsafe { } }", true)]
    public async Task ContainsFindsAnUnsafeOnlyFormAsync(string member, bool expected)
    {
        var declaration = SyntaxFactory.ParseMemberDeclaration(member)!;

        await Assert.That(UnsafeContextSyntax.Contains(declaration)).IsEqualTo(expected);
    }

    /// <summary>Verifies the node handed in is classified on its own but not counted as its own descendant.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TheNodeItselfIsNotItsOwnDescendantAsync()
    {
        var pointer = SyntaxFactory.ParseTypeName("int*");

        await Assert.That(UnsafeContextSyntax.RequiresUnsafeContext(pointer)).IsTrue();
        await Assert.That(UnsafeContextSyntax.Contains(pointer)).IsFalse();
    }
}
