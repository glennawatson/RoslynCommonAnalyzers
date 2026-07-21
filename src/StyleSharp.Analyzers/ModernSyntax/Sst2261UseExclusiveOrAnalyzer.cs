// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports exclusive-or written the long way (SST2261): <c>(x &amp;&amp; !y) || (!x &amp;&amp; y)</c>, and the
/// bitwise <c>(x &amp; !y) | (!x &amp; y)</c>, both mean <c>x ^ y</c>. Reported only when <c>x</c> and <c>y</c> are
/// side-effect-free boolean expressions, because the long form reads each of them twice and <c>^</c> reads each once.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2261UseExclusiveOrAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseExclusiveOr);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LogicalOrExpression);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.BitwiseOrExpression);
    }

    /// <summary>Matches the exclusive-or reimplementation and returns its two operands.</summary>
    /// <param name="binary">The <c>||</c> or <c>|</c> expression.</param>
    /// <param name="x">The first exclusive-or operand.</param>
    /// <param name="y">The second exclusive-or operand.</param>
    /// <returns><see langword="true"/> when the shape is a side-effect-free exclusive-or.</returns>
    internal static bool TryMatch(BinaryExpressionSyntax binary, out ExpressionSyntax x, out ExpressionSyntax y)
    {
        x = null!;
        y = null!;

        var andKind = binary.IsKind(SyntaxKind.LogicalOrExpression) ? SyntaxKind.LogicalAndExpression : SyntaxKind.BitwiseAndExpression;
        if (Unparenthesize(binary.Left) is not BinaryExpressionSyntax left || !left.IsKind(andKind)
            || Unparenthesize(binary.Right) is not BinaryExpressionSyntax right || !right.IsKind(andKind)
            || !TrySplit(left, out var positiveLeft, out var negatedLeft)
            || !TrySplit(right, out var positiveRight, out var negatedRight)
            || !IsPureMirror(positiveLeft, negatedLeft, positiveRight, negatedRight))
        {
            return false;
        }

        x = positiveLeft;
        y = negatedLeft;
        return true;
    }

    /// <summary>Returns whether the two conjunctions mirror each other over two side-effect-free values.</summary>
    /// <param name="positiveLeft">The non-negated operand of the left conjunction.</param>
    /// <param name="negatedLeft">The negated operand of the left conjunction.</param>
    /// <param name="positiveRight">The non-negated operand of the right conjunction.</param>
    /// <param name="negatedRight">The negated operand of the right conjunction.</param>
    /// <returns><see langword="true"/> when the operands form a side-effect-free exclusive-or.</returns>
    private static bool IsPureMirror(
        ExpressionSyntax positiveLeft,
        ExpressionSyntax negatedLeft,
        ExpressionSyntax positiveRight,
        ExpressionSyntax negatedRight)
        => SyntaxFactory.AreEquivalent(positiveLeft, negatedRight)
            && SyntaxFactory.AreEquivalent(negatedLeft, positiveRight)
            && SideEffectFreeExpression.IsSideEffectFree(positiveLeft)
            && SideEffectFreeExpression.IsSideEffectFree(negatedLeft);

    /// <summary>Reports an exclusive-or reimplementation over two boolean operands.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryMatch(binary, out var x, out var y)
            || !IsBoolean(context.SemanticModel, x, context.CancellationToken)
            || !IsBoolean(context.SemanticModel, y, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseExclusiveOr, binary.GetLocation()));
    }

    /// <summary>Splits a conjunction <c>p &amp;&amp; !q</c> (in either order) into its positive and negated operands.</summary>
    /// <param name="conjunction">The conjunction to split.</param>
    /// <param name="positive">The non-negated operand.</param>
    /// <param name="negated">The operand inside the logical negation.</param>
    /// <returns><see langword="true"/> when exactly one operand is a logical negation.</returns>
    private static bool TrySplit(BinaryExpressionSyntax conjunction, out ExpressionSyntax positive, out ExpressionSyntax negated)
    {
        positive = null!;
        negated = null!;

        var left = Unparenthesize(conjunction.Left);
        var right = Unparenthesize(conjunction.Right);
        var leftNegated = TryGetNegated(left, out var leftInner);
        var rightNegated = TryGetNegated(right, out var rightInner);
        if (leftNegated == rightNegated)
        {
            return false;
        }

        if (leftNegated)
        {
            negated = leftInner;
            positive = right;
        }
        else
        {
            negated = rightInner;
            positive = left;
        }

        return true;
    }

    /// <summary>Returns the operand of a logical negation.</summary>
    /// <param name="expression">The candidate negation.</param>
    /// <param name="inner">The negated operand, unparenthesized.</param>
    /// <returns><see langword="true"/> when the expression is a logical negation.</returns>
    private static bool TryGetNegated(ExpressionSyntax expression, out ExpressionSyntax inner)
    {
        if (expression is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation)
        {
            inner = Unparenthesize(negation.Operand);
            return true;
        }

        inner = null!;
        return false;
    }

    /// <summary>Returns whether an expression is of the non-nullable <see langword="bool"/> type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> for <see langword="bool"/>.</returns>
    private static bool IsBoolean(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken)
        => model.GetTypeInfo(expression, cancellationToken).Type?.SpecialType == SpecialType.System_Boolean;

    /// <summary>Strips redundant parentheses from an operand.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns>The operand with any surrounding parentheses removed.</returns>
    private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
