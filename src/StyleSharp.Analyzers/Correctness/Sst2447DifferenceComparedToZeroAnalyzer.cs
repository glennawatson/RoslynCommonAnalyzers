// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an integer subtraction compared against the literal zero (SST2447): <c>a - b &gt; 0</c> and its
/// mirror <c>0 &lt; a - b</c> read as <c>a &gt; b</c> but answer a different question once the difference
/// wraps.
/// </summary>
/// <remarks>
/// The clean path is syntactic: a comparison whose operands are not a subtraction and a zero literal is
/// dropped before the semantic model is consulted. Only then is the subtraction's type checked, and only an
/// integral one is reported — floating-point and decimal arithmetic produce no wrapped difference.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2447DifferenceComparedToZeroAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.DifferenceComparedToZero);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Strips redundant parentheses from an expression.</summary>
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

    /// <summary>Returns the operator text a direct comparison of the two operands would use.</summary>
    /// <param name="comparison">The comparison kind as written.</param>
    /// <param name="subtractionOnLeft">Whether the subtraction is the comparison's left operand.</param>
    /// <returns>The operator text for the rewritten comparison.</returns>
    /// <remarks>
    /// With the subtraction on the left the operator carries over unchanged; with the subtraction on the
    /// right the operands swap sides, so the relational operators mirror.
    /// </remarks>
    internal static string RewrittenOperatorText(SyntaxKind comparison, bool subtractionOnLeft)
    {
        var asWritten = comparison switch
        {
            SyntaxKind.GreaterThanExpression => ">",
            SyntaxKind.GreaterThanOrEqualExpression => ">=",
            SyntaxKind.LessThanExpression => "<",
            SyntaxKind.LessThanOrEqualExpression => "<=",
            SyntaxKind.EqualsExpression => "==",
            _ => "!=",
        };

        return subtractionOnLeft ? asWritten : Mirrored(asWritten);
    }

    /// <summary>Mirrors a comparison operator, for when the two operands change sides.</summary>
    /// <param name="text">The operator text as written.</param>
    /// <returns>The operator that means the same thing with the operands swapped.</returns>
    internal static string Mirrored(string text) => text switch
    {
        ">" => "<",
        ">=" => "<=",
        "<" => ">",
        "<=" => ">=",
        _ => text,
    };

    /// <summary>Returns whether an expression is the literal zero.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for a bare <c>0</c>.</returns>
    private static bool IsZeroLiteral(ExpressionSyntax expression)
        => Unwrap(expression) is LiteralExpressionSyntax { Token.Value: 0 };

    /// <summary>Reports one comparison of a difference against zero.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        var subtractionOnLeft = Unwrap(comparison.Left) is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.SubtractExpression };
        var subtractionSide = subtractionOnLeft ? comparison.Left : comparison.Right;
        var zeroSide = subtractionOnLeft ? comparison.Right : comparison.Left;

        if (Unwrap(subtractionSide) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.SubtractExpression } subtraction
            || !IsZeroLiteral(zeroSide))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(subtraction, context.CancellationToken).Type is not { } type
            || !IsIntegral(type.SpecialType))
        {
            return;
        }

        var text = RewrittenOperatorText((SyntaxKind)comparison.RawKind, subtractionOnLeft);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.DifferenceComparedToZero,
            comparison.SyntaxTree,
            comparison.Span,
            $"{subtraction.Left} {text} {subtraction.Right}"));
    }

    /// <summary>Returns whether a special type is one of the integer types whose subtraction wraps.</summary>
    /// <param name="specialType">The subtraction's special type.</param>
    /// <returns><see langword="true"/> for the built-in integer types.</returns>
    /// <remarks>
    /// <see cref="SpecialType"/> lists the eight integer types contiguously from <c>sbyte</c> to
    /// <c>ulong</c>, so the test is one range comparison. <c>char</c> sits just below the range and
    /// <c>decimal</c> just above it, and neither is wanted here: C# promotes a <c>char</c> subtraction to
    /// <c>int</c> before it can wrap, and <c>decimal</c> throws rather than wrapping.
    /// </remarks>
    private static bool IsIntegral(SpecialType specialType)
        => specialType is >= SpecialType.System_SByte and <= SpecialType.System_UInt64;
}
