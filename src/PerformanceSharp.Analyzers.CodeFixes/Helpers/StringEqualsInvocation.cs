// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>Builds a <c>string.Equals(left, right, comparison)</c> call.</summary>
internal static class StringEqualsInvocation
{
    /// <summary>Builds the call.</summary>
    /// <param name="left">The first string, written without its trivia.</param>
    /// <param name="right">The second string, written without its trivia.</param>
    /// <param name="comparison">The <c>StringComparison</c> argument, written as given.</param>
    /// <returns>The invocation, with a single space after each comma.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InvocationExpressionSyntax Build(ExpressionSyntax left, ExpressionSyntax right, ExpressionSyntax comparison) =>
        SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName(nameof(string.Equals))),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(new SyntaxNodeOrToken[]
            {
                SyntaxFactory.Argument(left.WithoutTrivia()),
                CommaWithTrailingSpace(),
                SyntaxFactory.Argument(right.WithoutTrivia()),
                CommaWithTrailingSpace(),
                SyntaxFactory.Argument(comparison),
            })));

    /// <summary>Creates a comma token followed by a single space.</summary>
    /// <returns>The comma token.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxToken CommaWithTrailingSpace() =>
        SyntaxFactory.Token(default, SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));
}
