// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Recognises the transposed-operator shape <c>x =+ 1</c> from its tokens alone. Shared by the correctness
/// rule that reports it (SST2417) and the spacing rules that must stay silent on the same span, so the
/// style fix does not erase the evidence the correctness rule needs.
/// </summary>
internal static class TransposedCompoundAssignment
{
    /// <summary>Returns whether an assignment is spaced like a transposed operator.</summary>
    /// <param name="assignment">The assignment to inspect.</param>
    /// <returns><see langword="true"/> for <c>x =+ 1</c> and its <c>=-</c> / <c>=!</c> siblings.</returns>
    /// <remarks>
    /// The tell is purely lexical: the <c>=</c> touches a following unary <c>+</c>, <c>-</c> or <c>!</c>
    /// with no space, and that unary operator is then followed by a space. <c>x =+ 1</c> and <c>x = +1</c>
    /// are the same tokens and differ only in where the space falls, so the asymmetry is the whole signal.
    /// </remarks>
    public static bool Matches(AssignmentExpressionSyntax assignment)
        => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && !assignment.OperatorToken.HasTrailingTrivia
            && assignment.Right is PrefixUnaryExpressionSyntax prefix
            && IsTransposableOperator(prefix.Kind())
            && prefix.OperatorToken.HasTrailingTrivia;

    /// <summary>Returns whether a prefix operator is one that transposes into an operator.</summary>
    /// <param name="kind">The prefix expression's syntax kind.</param>
    /// <returns><see langword="true"/> for unary <c>+</c>, <c>-</c> and <c>!</c>.</returns>
    private static bool IsTransposableOperator(SyntaxKind kind)
        => kind is SyntaxKind.UnaryPlusExpression
            or SyntaxKind.UnaryMinusExpression
            or SyntaxKind.LogicalNotExpression;
}
