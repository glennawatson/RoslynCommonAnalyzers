// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared type-parameter name writer.</summary>
public sealed class TypeParameterNamesUnitTest
{
    /// <summary>Verifies the names are joined by the separator, with nothing before the first or after the last.</summary>
    /// <param name="separator">The separator to join with.</param>
    /// <param name="expected">The expected appended text.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(",", "Cache{TKey,TValue}")]
    [Arguments(", ", "Cache{TKey, TValue}")]
    public async Task AppendJoinedJoinsTheNamesAsync(string separator, string expected)
    {
        var type = (ClassDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("class Cache<TKey, TValue> { }")!;

        var text = TypeParameterNames.AppendJoined(new("Cache{"), type.TypeParameterList!, separator).Append('}').ToString();

        await Assert.That(text).IsEqualTo(expected);
    }

    /// <summary>Verifies a single type parameter is written without a separator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendJoinedWritesASingleNameAloneAsync()
    {
        var type = (ClassDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("class Box<T> { }")!;

        await Assert.That(TypeParameterNames.AppendJoined(new(), type.TypeParameterList!, ", ").ToString()).IsEqualTo("T");
    }
}
