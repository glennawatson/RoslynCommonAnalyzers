// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests wrapping an expression in parentheses that keep its trivia.</summary>
public class ExpressionParenthesesTests
{
    /// <summary>Verifies the expression's trivia moves outside the parentheses.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TriviaMovesOutsideTheParenthesesAsync()
    {
        var expression = SyntaxFactory.ParseExpression("a + b").WithLeadingTrivia(SyntaxFactory.Comment("/*l*/")).WithTrailingTrivia(SyntaxFactory.Comment("/*t*/"));

        var wrapped = ExpressionParentheses.Wrap(expression);

        await Assert.That(wrapped.ToFullString()).IsEqualTo("/*l*/(a + b)/*t*/");
        await Assert.That(wrapped.Expression.HasLeadingTrivia).IsFalse();
        await Assert.That(wrapped.OpenParenToken.TrailingTrivia.Single().IsKind(SyntaxKind.WhitespaceTrivia)).IsTrue();
    }

    /// <summary>Verifies a bare expression is wrapped without adding visible text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BareExpressionIsWrappedAsync()
    {
        var wrapped = ExpressionParentheses.Wrap(SyntaxFactory.ParseExpression("x ?? y"));

        await Assert.That(wrapped).IsTypeOf<ParenthesizedExpressionSyntax>();
        await Assert.That(wrapped.ToFullString()).IsEqualTo("(x ?? y)");
    }
}
