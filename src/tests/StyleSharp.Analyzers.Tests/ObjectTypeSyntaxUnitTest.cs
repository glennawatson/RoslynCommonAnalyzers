// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared syntactic recognition of <c>System.Object</c>.</summary>
public sealed class ObjectTypeSyntaxUnitTest
{
    /// <summary>Verifies the spellings syntax alone can vouch for are recognized.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("object")]
    [Arguments("System.Object")]
    [Arguments("global::System.Object")]
    public async Task UnambiguousSpellingsAreObjectAsync(string type) =>
        await Assert.That(ObjectTypeSyntax.IsUnambiguousObjectType(SyntaxFactory.ParseTypeName(type))).IsTrue();

    /// <summary>Verifies a spelling a type in scope could shadow, or a different type, is not recognized.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Object")]
    [Arguments("Other.Object")]
    [Arguments("System.String")]
    [Arguments("string")]
    public async Task AmbiguousOrOtherSpellingsAreNotObjectAsync(string type) =>
        await Assert.That(ObjectTypeSyntax.IsUnambiguousObjectType(SyntaxFactory.ParseTypeName(type))).IsFalse();

    /// <summary>Verifies both spellings of the <c>System</c> namespace are recognized and nothing else is.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SystemNamespaceSpellingsAsync()
    {
        await Assert.That(ObjectTypeSyntax.IsSystemNamespace(SyntaxFactory.ParseName("System"))).IsTrue();
        await Assert.That(ObjectTypeSyntax.IsSystemNamespace(SyntaxFactory.ParseName("global::System"))).IsTrue();
        await Assert.That(ObjectTypeSyntax.IsSystemNamespace(SyntaxFactory.ParseName("Sys"))).IsFalse();
        await Assert.That(ObjectTypeSyntax.IsSystemNamespace(SyntaxFactory.ParseName("other::System"))).IsFalse();
    }
}
