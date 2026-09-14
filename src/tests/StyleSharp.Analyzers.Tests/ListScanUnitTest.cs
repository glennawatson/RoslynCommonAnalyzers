// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared existence scans over separated lists and arrays.</summary>
public sealed class ListScanUnitTest
{
    /// <summary>The prefixes the array scans test against.</summary>
    private static readonly string[] Prefixes = ["ghp_", "sk_live_"];

    /// <summary>Verifies a separated-list scan finds a matching node.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatedListFindsAMatchingNodeAsync()
    {
        var tuple = (TupleTypeSyntax)SyntaxFactory.ParseTypeName("(int First, string)");

        await Assert.That(ListScan.Any(tuple.Elements, static element => element.Identifier.IsKind(SyntaxKind.None))).IsTrue();
    }

    /// <summary>Verifies a separated-list scan with no matching node, or no nodes at all, is false.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatedListWithoutAMatchIsFalseAsync()
    {
        var tuple = (TupleTypeSyntax)SyntaxFactory.ParseTypeName("(int First, string Second)");

        await Assert.That(ListScan.Any(tuple.Elements, static element => element.Identifier.IsKind(SyntaxKind.None))).IsFalse();
        await Assert.That(ListScan.Any(default(SeparatedSyntaxList<TupleElementSyntax>), static _ => true)).IsFalse();
    }

    /// <summary>Verifies an array scan passes the value to each test and stops at a match.</summary>
    /// <param name="value">The value tested against every prefix.</param>
    /// <param name="expected">Whether a prefix matches.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("sk_live_0123", true)]
    [Arguments("ghp_abcdef", true)]
    [Arguments("pk_test_0123", false)]
    public async Task ArrayTestsEachEntryAgainstTheValueAsync(string value, bool expected) =>
        await Assert.That(ListScan.Any(Prefixes, value, static (prefix, candidate) => candidate.StartsWith(prefix, StringComparison.Ordinal))).IsEqualTo(expected);

    /// <summary>Verifies an empty array matches nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyArrayIsFalseAsync() =>
        await Assert.That(ListScan.Any(Array.Empty<string>(), "ghp_abcdef", static (_, _) => true)).IsFalse();
}
