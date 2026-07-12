// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Decides whether an expression can be evaluated twice — or not at all — without changing what the
/// program does. The rules that reason about two operands being "the same" depend on it: SST1474 must not
/// claim <c>Next() == Next()</c> compares one thing with itself, and SST1473 must not offer to rewrite such
/// a comparison as a NaN test.
/// </summary>
/// <remarks>
/// <para>
/// The whitelist is closed, so the question is never "does this node have a side effect" but "is every node
/// inside it known to have none". An invocation, an <c>await</c>, a <c>++</c>/<c>--</c>, an assignment, an
/// object creation and a lambda are all simply absent from the list, which rejects them wherever they
/// appear in the operand.
/// </para>
/// <para>
/// An element access is deliberately left out: <c>a[i]</c> runs an indexer this rule cannot see, so reading
/// it twice is not provably the same as reading it once. A member access is allowed even though a property
/// getter is a call, because a getter that changes observable state is already a bug the reader is entitled
/// to assume away — the same assumption every compiler optimization of a field-like property makes.
/// </para>
/// </remarks>
internal static class SideEffectFreeExpression
{
    /// <summary>Returns whether evaluating an expression cannot change observable state.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for names, literals, member-access chains, and pure operators over those.</returns>
    /// <remarks>
    /// Every <see cref="BinaryExpressionSyntax"/> kind is an operator that computes rather than mutates, so a
    /// binary node is pure exactly when both of its operands are, and no list of operator kinds is needed.
    /// Anything that is not one of the recursive shapes falls through to <see cref="IsPlainRead"/>, which is
    /// where the leaves — and the rejection of everything else — live.
    /// </remarks>
    public static bool IsSideEffectFree(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => IsSideEffectFree(member.Expression),
        ParenthesizedExpressionSyntax parenthesized => IsSideEffectFree(parenthesized.Expression),
        CastExpressionSyntax cast => IsSideEffectFree(cast.Expression),
        PrefixUnaryExpressionSyntax prefix => IsPureUnaryKind(prefix.Kind()) && IsSideEffectFree(prefix.Operand),
        BinaryExpressionSyntax binary => IsSideEffectFree(binary.Left) && IsSideEffectFree(binary.Right),
        _ => IsPlainRead(expression),
    };

    /// <summary>Returns whether an expression is a leaf that only reads a name or states a value.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for a name, a type, a literal, <see langword="this"/> and <see langword="base"/>.</returns>
    /// <remarks>
    /// <see cref="TypeSyntax"/> is the base of every plain and qualified name, so it covers <c>value</c>,
    /// <c>a.B</c>'s receiver, <c>int</c> in <c>int.MaxValue</c>, and the right operand of <c>is</c> / <c>as</c>
    /// in one test. Everything else — an invocation, an element access, an object creation, an assignment, an
    /// <c>await</c>, a lambda — is rejected by falling off the end.
    /// </remarks>
    private static bool IsPlainRead(ExpressionSyntax expression)
        => expression is TypeSyntax or LiteralExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax;

    /// <summary>Returns whether a prefix operator only reads its operand.</summary>
    /// <param name="kind">The prefix expression's syntax kind.</param>
    /// <returns><see langword="true"/> for <c>!</c>, <c>~</c>, <c>-</c> and <c>+</c>.</returns>
    /// <remarks><c>++</c>, <c>--</c>, <c>&amp;</c> and <c>*</c> are excluded: they mutate, or they reach through a pointer.</remarks>
    private static bool IsPureUnaryKind(SyntaxKind kind)
        => kind is SyntaxKind.LogicalNotExpression
            or SyntaxKind.BitwiseNotExpression
            or SyntaxKind.UnaryMinusExpression
            or SyntaxKind.UnaryPlusExpression;
}
