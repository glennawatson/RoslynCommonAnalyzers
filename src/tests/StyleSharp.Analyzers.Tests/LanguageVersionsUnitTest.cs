// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared language-version checks a rule gates suggested syntax on.</summary>
public sealed class LanguageVersionsUnitTest
{
    /// <summary>Verifies a tree satisfies its own version and every earlier one, and fails a later one.</summary>
    /// <param name="parsedAs">The version the tree is parsed with.</param>
    /// <param name="required">The version the check requires.</param>
    /// <param name="expected">Whether the tree satisfies it.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(LanguageVersion.CSharp10, LanguageVersion.CSharp9, true)]
    [Arguments(LanguageVersion.CSharp10, LanguageVersion.CSharp10, true)]
    [Arguments(LanguageVersion.CSharp9, LanguageVersion.CSharp10, false)]
    [Arguments(LanguageVersion.Preview, LanguageVersion.CSharp12, true)]
    public async Task IsAtLeastComparesTheParsedVersionAsync(LanguageVersion parsedAs, LanguageVersion required, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }", new(parsedAs));
        var root = await tree.GetRootAsync();

        await Assert.That(LanguageVersions.IsAtLeast(root, required)).IsEqualTo(expected);
    }
}
