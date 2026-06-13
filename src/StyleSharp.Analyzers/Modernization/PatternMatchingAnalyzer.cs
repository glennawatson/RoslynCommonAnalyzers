// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped modernization analyzer that steers older <c>as</c>/<c>is</c> idioms toward C# type patterns.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST2005 — an <c>as</c> cast compared to <c>null</c> (<c>x as T != null</c>) should be a type pattern.</description></item>
/// <item><description>SST2006 — a negated type test (<c>!(x is T)</c>) should use <c>is not</c>.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PatternMatchingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernizationRules.UseIsPatternOverAsNullCheck,
        ModernizationRules.UseNegatedIsPattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeAsNullComparison, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeNegatedIs, SyntaxKind.LogicalNotExpression);
    }

    /// <summary>Returns the <c>as</c> expression on either side of a null comparison, or <see langword="null"/>.</summary>
    /// <param name="comparison">The equality comparison.</param>
    /// <returns>The <c>as</c> expression compared to null, or <see langword="null"/> when the shape does not match.</returns>
    internal static BinaryExpressionSyntax? GetAsOperandComparedToNull(BinaryExpressionSyntax comparison)
    {
        if (IsNullLiteral(comparison.Right))
        {
            return AsCastOperand(comparison.Left);
        }

        if (!IsNullLiteral(comparison.Left))
        {
            return null;
        }

        return AsCastOperand(comparison.Right);
    }

    /// <summary>Unwraps any enclosing parentheses to reach the inner expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    internal static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Builds the <c>operand is not type</c> pattern expression with single-space spacing.</summary>
    /// <param name="operand">The value being tested.</param>
    /// <param name="type">The type being tested for.</param>
    /// <returns>An <c>is not</c> pattern expression.</returns>
    internal static IsPatternExpressionSyntax BuildIsNotPattern(ExpressionSyntax operand, TypeSyntax type)
    {
        var typePattern = SyntaxFactory.TypePattern(type.WithoutTrivia());
        var notPattern = SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space), typePattern);
        return SyntaxFactory.IsPatternExpression(
            operand.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.IsKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            notPattern);
    }

    /// <summary>Builds the <c>operand is type</c> expression with single-space spacing.</summary>
    /// <param name="operand">The value being tested.</param>
    /// <param name="type">The type being tested for.</param>
    /// <returns>An <c>is</c> type-test expression.</returns>
    internal static BinaryExpressionSyntax BuildIsTypeTest(ExpressionSyntax operand, TypeSyntax type)
    {
        var isOperator = SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.IsKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        return SyntaxFactory.BinaryExpression(SyntaxKind.IsExpression, operand.WithoutTrivia(), isOperator, type.WithoutTrivia());
    }

    /// <summary>Returns the operand as an <c>as</c> expression once parentheses are peeled, or <see langword="null"/>.</summary>
    /// <param name="operand">The comparison operand.</param>
    /// <returns>The <c>as</c> expression, or <see langword="null"/> when it is not one.</returns>
    private static BinaryExpressionSyntax? AsCastOperand(ExpressionSyntax operand)
        => Unwrap(operand) is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } asExpression ? asExpression : null;

    /// <summary>Reports SST2005 when an <c>as</c> cast is compared to <c>null</c> with a reference type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeAsNullComparison(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (GetAsOperandComparedToNull(comparison) is not { } asExpression)
        {
            return;
        }

        // 'x is T' is only equivalent for reference types; 'x as int?' would need a different pattern.
        var targetType = context.SemanticModel.GetTypeInfo(asExpression.Right, context.CancellationToken).Type;
        if (targetType?.IsReferenceType != true)
        {
            return;
        }

        var suggestion = comparison.IsKind(SyntaxKind.NotEqualsExpression) ? "is" : "is not";
        context.ReportDiagnostic(Diagnostic.Create(ModernizationRules.UseIsPatternOverAsNullCheck, comparison.GetLocation(), suggestion));
    }

    /// <summary>Reports SST2006 when a <c>!</c> wraps a type test (<c>!(x is T)</c>).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeNegatedIs(SyntaxNodeAnalysisContext context)
    {
        var not = (PrefixUnaryExpressionSyntax)context.Node;

        // Only the type-test form ('x is T') negates cleanly; declaration patterns ('x is T t') bind a name.
        if (Unwrap(not.Operand) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpression
            || isExpression.Right is not TypeSyntax)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernizationRules.UseNegatedIsPattern, not.GetLocation()));
    }

    /// <summary>Returns whether an expression is the <c>null</c> literal.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> for a <c>null</c> literal.</returns>
    private static bool IsNullLiteral(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);
}
