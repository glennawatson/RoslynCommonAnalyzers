// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Direct tests for <see cref="EmptinessComparisonClassifier"/>, the count-comparison arithmetic
/// shared by PSH1117, PSH1119, and PSH1126.
/// </summary>
public class EmptinessComparisonClassifierUnitTest
{
    /// <summary>Verifies only the literals <c>0</c> and <c>1</c> are recognized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetZeroOrOneLiteralRecognizesOnlyZeroAndOneAsync()
    {
        await Assert.That(EmptinessComparisonClassifier.TryGetZeroOrOneLiteral(Expr("0"))).IsEqualTo(0);
        await Assert.That(EmptinessComparisonClassifier.TryGetZeroOrOneLiteral(Expr("1"))).IsEqualTo(1);
        await Assert.That(EmptinessComparisonClassifier.TryGetZeroOrOneLiteral(Expr("2"))).IsNull();
        await Assert.That(EmptinessComparisonClassifier.TryGetZeroOrOneLiteral(Expr("count"))).IsNull();
    }

    /// <summary>Verifies the comparison kind is mirrored for reversed operand order and left untouched otherwise.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MirrorComparisonSwapsRelationalOperatorsOnlyAsync()
    {
        await Assert.That(EmptinessComparisonClassifier.MirrorComparison(SyntaxKind.LessThanExpression)).IsEqualTo(SyntaxKind.GreaterThanExpression);
        await Assert.That(EmptinessComparisonClassifier.MirrorComparison(SyntaxKind.GreaterThanOrEqualExpression)).IsEqualTo(SyntaxKind.LessThanOrEqualExpression);
        await Assert.That(EmptinessComparisonClassifier.MirrorComparison(SyntaxKind.EqualsExpression)).IsEqualTo(SyntaxKind.EqualsExpression);
        await Assert.That(EmptinessComparisonClassifier.MirrorComparison(SyntaxKind.NotEqualsExpression)).IsEqualTo(SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Verifies each recognized count-on-the-left shape maps to the right emptiness meaning.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassifyHasElementsMapsEveryRecognizedShapeAsync()
    {
        await Assert.That(EmptinessComparisonClassifier.ClassifyHasElements(SyntaxKind.GreaterThanExpression, 0) is true).IsTrue();
        await Assert.That(EmptinessComparisonClassifier.ClassifyHasElements(SyntaxKind.GreaterThanOrEqualExpression, 1) is true).IsTrue();
        await Assert.That(EmptinessComparisonClassifier.ClassifyHasElements(SyntaxKind.NotEqualsExpression, 0) is true).IsTrue();
        await Assert.That(EmptinessComparisonClassifier.ClassifyHasElements(SyntaxKind.EqualsExpression, 0) is false).IsTrue();
        await Assert.That(EmptinessComparisonClassifier.ClassifyHasElements(SyntaxKind.LessThanExpression, 1) is false).IsTrue();
        await Assert.That(EmptinessComparisonClassifier.ClassifyHasElements(SyntaxKind.LessThanOrEqualExpression, 0) is false).IsTrue();
        await Assert.That(EmptinessComparisonClassifier.ClassifyHasElements(SyntaxKind.GreaterThanExpression, 1)).IsNull();
        await Assert.That(EmptinessComparisonClassifier.ClassifyHasElements(SyntaxKind.EqualsExpression, 1)).IsNull();
    }

    /// <summary>Verifies a count on the left is classified as "has elements".</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassifyReadsCountOnTheLeftAsync()
    {
        var binary = Binary("xs.Count() > 0");
        var shape = EmptinessComparisonClassifier.Classify(binary, (InvocationExpressionSyntax)binary.Left, null);

        await Assert.That(shape.HasValue).IsTrue();
        await Assert.That(shape!.Value.HasElements).IsTrue();
        await Assert.That(shape.Value.Count.ToString()).IsEqualTo("xs.Count()");
    }

    /// <summary>Verifies a count on the right is mirrored before classification.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassifyMirrorsWhenCountIsOnTheRightAsync()
    {
        var binary = Binary("0 < xs.Count()");
        var shape = EmptinessComparisonClassifier.Classify(binary, null, (InvocationExpressionSyntax)binary.Right);

        await Assert.That(shape.HasValue).IsTrue();
        await Assert.That(shape!.Value.HasElements).IsTrue();
    }

    /// <summary>Verifies comparisons that are not emptiness checks return no shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassifyReturnsNullForNonEmptinessComparisonsAsync()
    {
        var literalTooBig = Binary("xs.Count() > 5");
        await Assert.That(EmptinessComparisonClassifier.Classify(literalTooBig, (InvocationExpressionSyntax)literalTooBig.Left, null).HasValue).IsFalse();

        var equalsOne = Binary("xs.Count() == 1");
        await Assert.That(EmptinessComparisonClassifier.Classify(equalsOne, (InvocationExpressionSyntax)equalsOne.Left, null).HasValue).IsFalse();
    }

    /// <summary>Parses an expression fragment.</summary>
    /// <param name="text">The expression source.</param>
    /// <returns>The parsed expression.</returns>
    private static ExpressionSyntax Expr(string text) => SyntaxFactory.ParseExpression(text);

    /// <summary>Parses a binary comparison expression.</summary>
    /// <param name="text">The expression source.</param>
    /// <returns>The parsed binary expression.</returns>
    private static BinaryExpressionSyntax Binary(string text) => (BinaryExpressionSyntax)SyntaxFactory.ParseExpression(text);
}
