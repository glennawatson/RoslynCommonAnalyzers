// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a comparison of a non-nullable boolean expression to a <c>true</c>/<c>false</c>
/// literal (SST1143) — <c>x == true</c>, <c>x != false</c>, and so on — which is always
/// redundant. After the cheap syntactic check (exactly one side is a boolean literal) the
/// non-literal operand's type is confirmed to be <see cref="bool"/>: a <c>bool?</c> comparison
/// such as <c>task?.IsCompleted != false</c> folds <see langword="null"/> into the result and
/// cannot be reduced to the operand, so it is left alone.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BooleanLiteralComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.NoBooleanLiteralComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Returns whether an expression is the <c>true</c> or <c>false</c> literal.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> for a boolean literal.</returns>
    internal static bool IsBooleanLiteral(ExpressionSyntax expression)
        => expression.IsKind(SyntaxKind.TrueLiteralExpression) || expression.IsKind(SyntaxKind.FalseLiteralExpression);

    /// <summary>
    /// Returns whether an expression's result type is syntactically guaranteed to be a non-nullable
    /// <see cref="bool"/> — a logical, relational, equality, or pattern operator — so the semantic
    /// model can be skipped. Identifiers, member accesses, invocations, and casts are not certain
    /// (they may be <c>bool?</c>) and return <see langword="false"/>.
    /// </summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> when the result is certainly a non-nullable boolean.</returns>
    [SuppressMessage("Critical Code Smell", "S1541:Methods and properties should not be too complex", Justification = "A flat operator-kind switch here is a zero-allocation jump table.")]
    internal static bool IsCertainlyBoolean(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => IsCertainlyBoolean(parenthesized.Expression),
        PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.LogicalNotExpression),
        IsPatternExpressionSyntax => true,
        BinaryExpressionSyntax binary => binary.Kind() is SyntaxKind.LogicalAndExpression
            or SyntaxKind.LogicalOrExpression
            or SyntaxKind.EqualsExpression
            or SyntaxKind.NotEqualsExpression
            or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.IsExpression,
        _ => false
    };

    /// <summary>Reports SST1143 when exactly one side of an equality is a boolean literal.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        var leftLiteral = IsBooleanLiteral(binary.Left);
        var rightLiteral = IsBooleanLiteral(binary.Right);
        if (leftLiteral == rightLiteral)
        {
            return;
        }

        // The comparison is only redundant when the other operand is a plain bool. A bool? (e.g.
        // 'task?.IsCompleted != false') folds null into the result, so 'x == true' is not 'x'. Operands
        // whose result is syntactically a non-nullable bool skip the semantic model; the rest are bound.
        var nonLiteral = leftLiteral ? binary.Right : binary.Left;
        if (!IsCertainlyBoolean(nonLiteral)
            && context.SemanticModel.GetTypeInfo(nonLiteral, context.CancellationToken).Type is not { SpecialType: SpecialType.System_Boolean })
        {
            return;
        }

        var literal = leftLiteral ? binary.Left : binary.Right;
        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoBooleanLiteralComparison, binary.GetLocation(), literal.ToString()));
    }
}
