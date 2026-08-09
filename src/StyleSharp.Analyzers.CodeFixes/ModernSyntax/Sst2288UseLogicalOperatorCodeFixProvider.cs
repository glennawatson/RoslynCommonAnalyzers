// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a conditional with one boolean-literal branch as the logical operator it already is (SST2288):
/// <c>a ? b : false</c> becomes <c>a &amp;&amp; b</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2288UseLogicalOperatorCodeFixProvider))]
[Shared]
public sealed class Sst2288UseLogicalOperatorCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(ModernSyntaxRules.UseLogicalOperatorOverConditional.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use the logical operator",
            nameof(Sst2288UseLogicalOperatorCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported conditional and replaces it with the equivalent logical expression.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<ConditionalExpressionSyntax>() is not { } conditional
            || !Sst2288UseLogicalOperatorAnalyzer.TryClassify(conditional, out var negate, out var conjunction, out var other))
        {
            return null;
        }

        var left = negate ? Negate(conditional.Condition) : conditional.Condition.WithoutTrivia();
        var operatorToken = SyntaxFactory
            .Token(conjunction ? SyntaxKind.AmpersandAmpersandToken : SyntaxKind.BarBarToken)
            .WithLeadingTrivia(SyntaxFactory.Space)
            .WithTrailingTrivia(SyntaxFactory.Space);

        var replacement = SyntaxFactory.BinaryExpression(
                conjunction ? SyntaxKind.LogicalAndExpression : SyntaxKind.LogicalOrExpression,
                left,
                operatorToken,
                Parenthesize(other.WithoutTrivia(), conjunction))
            .WithTriviaFrom(conditional);

        return new NodeReplacement(conditional, replacement);
    }

    /// <summary>Negates a condition for the forms whose literal branch is the true one.</summary>
    /// <param name="condition">The condition to negate.</param>
    /// <returns>The negated condition.</returns>
    /// <remarks>
    /// A leading <c>!</c> is dropped rather than doubled; anything that is not already a primary expression is
    /// parenthesized so the <c>!</c> binds to the whole condition.
    /// </remarks>
    private static ExpressionSyntax Negate(ExpressionSyntax condition)
    {
        var inner = ExpressionSimplificationAnalyzer.Unwrap(condition);
        if (inner is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation)
        {
            return ExpressionSimplificationAnalyzer.Unwrap(negation.Operand).WithoutTrivia();
        }

        var operand = PrimaryExpressionClassification.IsPrimary(inner)
            ? inner.WithoutTrivia()
            : SyntaxFactory.ParenthesizedExpression(inner.WithoutTrivia());
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand);
    }

    /// <summary>Parenthesizes an operand whose own operator would regroup under the one being built.</summary>
    /// <param name="operand">The non-literal branch.</param>
    /// <param name="conjunction">Whether the expression being built is a conjunction.</param>
    /// <returns>The operand, parenthesized when precedence requires it.</returns>
    private static ExpressionSyntax Parenthesize(ExpressionSyntax operand, bool conjunction)
        => conjunction && operand.IsKind(SyntaxKind.LogicalOrExpression)
            ? SyntaxFactory.ParenthesizedExpression(operand)
            : operand;
}
