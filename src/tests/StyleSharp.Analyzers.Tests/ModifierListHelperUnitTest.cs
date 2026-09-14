// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared modifier-list scans.</summary>
public sealed class ModifierListHelperUnitTest
{
    /// <summary>Verifies the five-kind scan finds any one of the requested kinds wherever it sits in the list.</summary>
    /// <param name="modifier">The modifier present alongside <c>static</c>.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(SyntaxKind.PublicKeyword)]
    [Arguments(SyntaxKind.PrivateKeyword)]
    [Arguments(SyntaxKind.ProtectedKeyword)]
    [Arguments(SyntaxKind.InternalKeyword)]
    [Arguments(SyntaxKind.FileKeyword)]
    public async Task ContainsAnyFindsEachRequestedKindAsync(SyntaxKind modifier)
    {
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(modifier));

        await Assert.That(ModifierListHelper.ContainsAny(
            modifiers,
            SyntaxKind.PublicKeyword,
            SyntaxKind.PrivateKeyword,
            SyntaxKind.ProtectedKeyword,
            SyntaxKind.InternalKeyword,
            SyntaxKind.FileKeyword)).IsTrue();
    }

    /// <summary>Verifies the five-kind scan is false when no requested kind is present, including for an empty list.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsAnyWithoutARequestedKindIsFalseAsync()
    {
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        await Assert.That(ModifierListHelper.ContainsAny(
            modifiers,
            SyntaxKind.OverrideKeyword,
            SyntaxKind.VirtualKeyword,
            SyntaxKind.AbstractKeyword,
            SyntaxKind.PartialKeyword,
            SyntaxKind.ExternKeyword)).IsFalse();
        await Assert.That(ModifierListHelper.ContainsAny(
            default,
            SyntaxKind.OverrideKeyword,
            SyntaxKind.VirtualKeyword,
            SyntaxKind.AbstractKeyword,
            SyntaxKind.PartialKeyword,
            SyntaxKind.ExternKeyword)).IsFalse();
    }
}
