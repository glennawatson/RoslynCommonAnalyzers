// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Classifies an integer count comparison against a <c>0</c> or <c>1</c> literal as an emptiness
/// test, shared by the rules that rewrite such comparisons (PSH1117, PSH1119, PSH1126). It
/// recognizes both operand orders and the six literal forms (<c>&gt; 0</c>, <c>&gt;= 1</c>,
/// <c>!= 0</c>, <c>== 0</c>, <c>&lt; 1</c>, <c>&lt;= 0</c>), reporting whether the comparison means
/// the sequence <em>has elements</em>. A caller that frames its result as "is empty" negates the
/// answer. The classification is syntactic and allocation-free.
/// </summary>
internal static class EmptinessComparisonClassifier
{
    /// <summary>Classifies an emptiness-shaped count comparison from its pre-extracted count operands.</summary>
    /// <typeparam name="TCount">The syntax node a caller treats as the counting operand.</typeparam>
    /// <param name="binary">The comparison to classify.</param>
    /// <param name="leftCount">The counting operand on the left, or <see langword="null"/>.</param>
    /// <param name="rightCount">The counting operand on the right, or <see langword="null"/>.</param>
    /// <returns>The counting operand and whether the check means "has elements", or <see langword="null"/>.</returns>
    public static (TCount Count, bool HasElements)? Classify<TCount>(BinaryExpressionSyntax binary, TCount? leftCount, TCount? rightCount)
        where TCount : ExpressionSyntax
    {
        if (leftCount is not null && TryGetZeroOrOneLiteral(binary.Right) is { } rightLiteral)
        {
            return ClassifyHasElements(binary.Kind(), rightLiteral) is { } hasElements ? (leftCount, hasElements) : null;
        }

        if (rightCount is not null && TryGetZeroOrOneLiteral(binary.Left) is { } leftLiteral)
        {
            return ClassifyHasElements(MirrorComparison(binary.Kind()), leftLiteral) is { } hasElements ? (rightCount, hasElements) : null;
        }

        return null;
    }

    /// <summary>Returns the integer value of a zero or one literal operand.</summary>
    /// <param name="expression">The comparison operand.</param>
    /// <returns>0 or 1, or <see langword="null"/>.</returns>
    public static int? TryGetZeroOrOneLiteral(ExpressionSyntax expression)
    {
        if (expression is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal)
        {
            return null;
        }

        return literal.Token.ValueText switch
        {
            "0" => 0,
            "1" => 1,
            _ => null,
        };
    }

    /// <summary>Mirrors a comparison kind for reversed operand order.</summary>
    /// <param name="kind">The original comparison kind.</param>
    /// <returns>The kind with the counting operand on the left.</returns>
    public static SyntaxKind MirrorComparison(SyntaxKind kind)
        => kind switch
        {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind,
        };

    /// <summary>Maps a count-on-the-left comparison to whether it means the sequence has elements.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <param name="literal">The literal operand value.</param>
    /// <returns><see langword="true"/> for "has elements", <see langword="false"/> for "is empty", or <see langword="null"/> when the shape needs the real count.</returns>
    public static bool? ClassifyHasElements(SyntaxKind kind, int literal)
        => (kind, literal) switch
        {
            (SyntaxKind.GreaterThanExpression, 0) => true,
            (SyntaxKind.GreaterThanOrEqualExpression, 1) => true,
            (SyntaxKind.NotEqualsExpression, 0) => true,
            (SyntaxKind.EqualsExpression, 0) => false,
            (SyntaxKind.LessThanExpression, 1) => false,
            (SyntaxKind.LessThanOrEqualExpression, 0) => false,
            _ => null,
        };
}
