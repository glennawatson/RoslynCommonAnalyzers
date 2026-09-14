// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests finding the binary expression a diagnostic was reported on.</summary>
public class EnclosingBinaryExpressionTests
{
    /// <summary>A method holding nested binary expressions of several kinds.</summary>
    private const string Source = "class C { bool M(bool a, bool b, bool c) => a || (b && c); }";

    /// <summary>Verifies the nearest binary expression of an accepted kind is returned.</summary>
    /// <param name="reported">The text the diagnostic covers.</param>
    /// <param name="kind">One accepted kind.</param>
    /// <param name="alternateKind">The other accepted kind.</param>
    /// <param name="expected">The expected binary expression text, or an empty string for none.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("b && c", SyntaxKind.LogicalAndExpression, SyntaxKind.BitwiseOrExpression, "b && c")]
    [Arguments("b && c", SyntaxKind.BitwiseOrExpression, SyntaxKind.LogicalOrExpression, "a || (b && c)")]
    [Arguments("c", SyntaxKind.LogicalOrExpression, SyntaxKind.LogicalAndExpression, "b && c")]
    [Arguments("c", SyntaxKind.ExclusiveOrExpression, SyntaxKind.BitwiseAndExpression, "")]
    public async Task FindsNearestAcceptedKindAsync(string reported, SyntaxKind kind, SyntaxKind alternateKind, string expected)
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var root = await tree.GetRootAsync();
        var start = Source.LastIndexOf(reported, StringComparison.Ordinal);
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UseExclusiveOr, Location.Create(tree, new(start, reported.Length)));

        var found = EnclosingBinaryExpression.Find(root, diagnostic, kind, alternateKind);

        await Assert.That(found?.ToString() ?? string.Empty).IsEqualTo(expected);
    }
}
