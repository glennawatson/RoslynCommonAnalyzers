// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a null check that sits beside an <c>is</c> type pattern on the same value (SST2018): the pattern
/// already excludes null, so the null check decides nothing.
/// </summary>
/// <remarks>
/// <para>Three shapes are recognised, all reducing to a single pattern test:</para>
/// <list type="bullet">
/// <item><description><c>o != null &amp;&amp; o is T</c> and <c>o is not null &amp;&amp; o is T</c> reduce to <c>o is T</c>.</description></item>
/// <item><description><c>o == null || o is not T</c> reduces to <c>o is not T</c>.</description></item>
/// <item><description>the combinator form <c>o is not null and T</c> reduces to <c>o is T</c>.</description></item>
/// </list>
/// <para>
/// A genuine "non-null but not a T" check (<c>o != null &amp;&amp; o is not T</c>) is left alone. The clean
/// path is a syntax-kind test that rejects every ordinary <c>&amp;&amp;</c>, and operand equivalence is a
/// token compare, not a semantic one.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2018RedundantNullCheckBesidePatternAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.RedundantNullCheckBesidePattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLogical, SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression);
        context.RegisterSyntaxNodeAction(AnalyzePattern, SyntaxKind.IsPatternExpression);
    }

    /// <summary>Reports a null check combined with a pattern test using <c>&amp;&amp;</c> or <c>||</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeLogical(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryGetTypeTest(binary.Right, out var patternReceiver, out var negated))
        {
            return;
        }

        var isAnd = binary.IsKind(SyntaxKind.LogicalAndExpression);
        var nullReceiver = isAnd ? GetNonNullReceiver(binary.Left) : GetNullReceiver(binary.Left);

        // '&&' pairs a non-null check with a positive type test; '||' pairs a null check with a negated one.
        if (nullReceiver is null || negated == isAnd || !SameSideEffectFree(nullReceiver, patternReceiver))
        {
            return;
        }

        var name = negated ? "is not" : "is";
        context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.RedundantNullCheckBesidePattern, binary.GetLocation(), name));
    }

    /// <summary>Reports the combinator form <c>o is not null and T</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzePattern(SyntaxNodeAnalysisContext context)
    {
        var isPattern = (IsPatternExpressionSyntax)context.Node;
        if (isPattern.Pattern is not BinaryPatternSyntax { RawKind: (int)SyntaxKind.AndPattern } and || !HasNotNullAndType(and))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.RedundantNullCheckBesidePattern, isPattern.GetLocation(), "is"));
    }

    /// <summary>Returns whether an <c>and</c> pattern is <c>not null</c> combined with a type pattern.</summary>
    /// <param name="and">The <c>and</c> pattern.</param>
    /// <returns><see langword="true"/> when one arm is <c>not null</c> and the other a positive type pattern.</returns>
    private static bool HasNotNullAndType(BinaryPatternSyntax and)
        => (IsNotNullPattern(and.Left) && IsTypePattern(and.Right))
            || (IsNotNullPattern(and.Right) && IsTypePattern(and.Left));

    /// <summary>Gets the receiver of a non-null assertion (<c>x != null</c> or <c>x is not null</c>).</summary>
    /// <param name="expression">The candidate assertion.</param>
    /// <returns>The receiver, or <see langword="null"/> when the expression is not a non-null assertion.</returns>
    private static ExpressionSyntax? GetNonNullReceiver(ExpressionSyntax expression) => expression switch
    {
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression } binary => NonNullOperand(binary),
        IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax unary } isPattern when IsNotNullPattern(unary) => isPattern.Expression,
        _ => null,
    };

    /// <summary>Gets the receiver of a null assertion (<c>x == null</c> or <c>x is null</c>).</summary>
    /// <param name="expression">The candidate assertion.</param>
    /// <returns>The receiver, or <see langword="null"/> when the expression is not a null assertion.</returns>
    private static ExpressionSyntax? GetNullReceiver(ExpressionSyntax expression) => expression switch
    {
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } binary => NonNullOperand(binary),
        IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax constant } isPattern when IsNullLiteral(constant.Expression) => isPattern.Expression,
        _ => null,
    };

    /// <summary>Gets the non-null operand of an equality comparison against <c>null</c>.</summary>
    /// <param name="binary">The equality comparison.</param>
    /// <returns>The other operand, or <see langword="null"/> when neither side is <c>null</c>.</returns>
    private static ExpressionSyntax? NonNullOperand(BinaryExpressionSyntax binary)
    {
        if (IsNullLiteral(binary.Right))
        {
            return binary.Left;
        }

        return IsNullLiteral(binary.Left) ? binary.Right : null;
    }

    /// <summary>Reads a type test, distinguishing <c>x is T</c> from <c>x is not T</c>.</summary>
    /// <param name="expression">The candidate <c>is</c> expression.</param>
    /// <param name="receiver">The tested receiver.</param>
    /// <param name="negated">Whether the type test is negated.</param>
    /// <returns><see langword="true"/> for a type test on a type pattern.</returns>
    private static bool TryGetTypeTest(ExpressionSyntax expression, out ExpressionSyntax receiver, out bool negated)
    {
        receiver = null!;
        negated = false;

        // A bare 'o is T' type check is an is-expression, not an is-pattern expression.
        if (expression is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } typeCheck)
        {
            receiver = typeCheck.Left;
            return true;
        }

        if (expression is not IsPatternExpressionSyntax isPattern)
        {
            return false;
        }

        if (isPattern.Pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } unary && IsTypePattern(unary.Pattern))
        {
            receiver = isPattern.Expression;
            negated = true;
            return true;
        }

        if (!IsTypePattern(isPattern.Pattern))
        {
            return false;
        }

        receiver = isPattern.Expression;
        return true;
    }

    /// <summary>Returns whether a pattern is a <c>not null</c> pattern.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true"/> for <c>not null</c>.</returns>
    private static bool IsNotNullPattern(PatternSyntax pattern)
        => pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern, Pattern: ConstantPatternSyntax constant }
            && IsNullLiteral(constant.Expression);

    /// <summary>Returns whether a pattern is a type pattern (<c>T</c> or <c>T x</c>).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true"/> for a type or declaration pattern.</returns>
    private static bool IsTypePattern(PatternSyntax pattern)
        => pattern is TypePatternSyntax or DeclarationPatternSyntax;

    /// <summary>Returns whether an expression is the <c>null</c> literal.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns><see langword="true"/> for <c>null</c>.</returns>
    private static bool IsNullLiteral(ExpressionSyntax expression)
        => expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns whether two receivers are the same side-effect-free expression.</summary>
    /// <param name="first">The first receiver.</param>
    /// <param name="second">The second receiver.</param>
    /// <returns><see langword="true"/> when reading either twice is provably the same.</returns>
    private static bool SameSideEffectFree(ExpressionSyntax first, ExpressionSyntax second)
        => SideEffectFreeExpression.IsSideEffectFree(first)
            && SyntaxFactory.AreEquivalent(first, second, topLevel: false);
}
