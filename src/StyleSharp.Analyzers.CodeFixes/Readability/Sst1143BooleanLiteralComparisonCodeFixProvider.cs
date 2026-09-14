// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant comparison to a boolean literal (SST1143), negating when needed.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1143BooleanLiteralComparisonCodeFixProvider))]
[Shared]
public sealed class Sst1143BooleanLiteralComparisonCodeFixProvider : CodeFixProvider
{
    /// <summary>Expression kinds a prefix <c>!</c> binds to without needing parentheses.</summary>
    private static readonly HashSet<SyntaxKind> NegationSafeKinds =
    [
        SyntaxKind.IdentifierName,
        SyntaxKind.SimpleMemberAccessExpression,
        SyntaxKind.InvocationExpression,
        SyntaxKind.ElementAccessExpression,
        SyntaxKind.ParenthesizedExpression,
        SyntaxKind.ThisExpression,
        SyntaxKind.BaseExpression,
        SyntaxKind.TrueLiteralExpression,
        SyntaxKind.FalseLiteralExpression,
        SyntaxKind.LogicalNotExpression,
        SyntaxKind.ConditionalAccessExpression
    ];

    /// <summary>Batches this fix's edits across a document, simplifying each comparison from the form nested edits left it in.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(
        ReportedNode.Find<BinaryExpressionSyntax>,
        static (current, _) => Simplify((BinaryExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoBooleanLiteralComparison.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Remove the comparison",
            nameof(Sst1143BooleanLiteralComparisonCodeFixProvider),
            ReportedNode.Find<BinaryExpressionSyntax>,
            Simplify);

    /// <summary>Reduces a boolean-literal comparison to the operand, negated when the comparison was, keeping the comparison's trivia.</summary>
    /// <param name="binary">The equality expression.</param>
    /// <returns>The simplified expression.</returns>
    internal static ExpressionSyntax Simplify(BinaryExpressionSyntax binary)
    {
        var leftLiteral = Sst1143BooleanLiteralComparisonAnalyzer.IsBooleanLiteral(binary.Left);
        var literal = leftLiteral ? binary.Left : binary.Right;
        var operand = (leftLiteral ? binary.Right : binary.Left).WithoutTrivia();

        // 'x == true' / 'x != false' keep the operand; 'x == false' / 'x != true' negate it.
        var literalIsTrue = literal.IsKind(SyntaxKind.TrueLiteralExpression);
        var isEquals = binary.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken);
        var simplified = literalIsTrue == isEquals ? operand : Negate(operand);
        return simplified.WithTriviaFrom(binary);
    }

    /// <summary>Returns the logical negation of an operand, parenthesizing it when precedence requires.</summary>
    /// <param name="operand">The operand to negate.</param>
    /// <returns>The negated expression.</returns>
    private static PrefixUnaryExpressionSyntax Negate(ExpressionSyntax operand)
    {
        var inner = NegationSafeKinds.Contains(operand.Kind()) ? operand : SyntaxFactory.ParenthesizedExpression(operand);
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, inner);
    }
}
