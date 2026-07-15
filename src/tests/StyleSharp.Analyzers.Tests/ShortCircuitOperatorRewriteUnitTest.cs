// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared eager-to-short-circuiting boolean operator rewrite.</summary>
public sealed class ShortCircuitOperatorRewriteUnitTest
{
    /// <summary>Verifies the eager bitwise operators are recognized and the short-circuiting ones are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsFixableKindMatchesOnlyEagerBooleanOperatorsAsync()
    {
        await Assert.That(ShortCircuitOperatorRewrite.IsFixableKind(ParseBinary("a & b"))).IsTrue();
        await Assert.That(ShortCircuitOperatorRewrite.IsFixableKind(ParseBinary("a | b"))).IsTrue();
        await Assert.That(ShortCircuitOperatorRewrite.IsFixableKind(ParseBinary("a && b"))).IsFalse();
        await Assert.That(ShortCircuitOperatorRewrite.IsFixableKind(ParseBinary("a ^ b"))).IsFalse();
    }

    /// <summary>Verifies the rewrite turns eager operators into their short-circuiting form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RewriteProducesShortCircuitingOperatorAsync()
    {
        await Assert.That(ShortCircuitOperatorRewrite.Rewrite(ParseBinary("a & b")).ToString()).IsEqualTo("a && b");
        await Assert.That(ShortCircuitOperatorRewrite.Rewrite(ParseBinary("a | b")).ToString()).IsEqualTo("a || b");
    }

    /// <summary>Verifies the rewrite parenthesizes when a tighter-binding bitwise parent would otherwise capture it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RewriteParenthesizesUnderTighterBitwiseParentAsync()
    {
        // Parses as (a & b) & c: rewriting the inner "a & b" must keep its grouping under the outer "& c".
        var inner = (BinaryExpressionSyntax)ParseBinary("a & b & c").Left;

        var rewritten = ShortCircuitOperatorRewrite.Rewrite(inner);

        await Assert.That(rewritten is ParenthesizedExpressionSyntax).IsTrue();
        await Assert.That(rewritten.ToString()).IsEqualTo("(a && b)");
    }

    /// <summary>Parses an expression as a binary expression.</summary>
    /// <param name="expression">The expression source.</param>
    /// <returns>The parsed binary expression.</returns>
    private static BinaryExpressionSyntax ParseBinary(string expression)
        => (BinaryExpressionSyntax)SyntaxFactory.ParseExpression(expression);
}
