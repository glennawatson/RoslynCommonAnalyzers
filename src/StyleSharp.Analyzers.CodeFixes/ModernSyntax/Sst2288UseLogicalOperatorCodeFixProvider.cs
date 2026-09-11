// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(ModernSyntaxRules.UseLogicalOperatorOverConditional.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use the logical operator",
            nameof(Sst2288UseLogicalOperatorCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

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

        // A conditional written across several lines ends its condition with a line break, and that break
        // is kept so the result stays spread rather than collapsing into one very long line.
        var conditionTrailing = conditional.Condition.GetTrailingTrivia();
        var wrapped = ContainsLineBreak(conditionTrailing);

        var left = negate ? Negate(conditional.Condition) : conditional.Condition.WithoutTrivia();
        if (wrapped)
        {
            left = left.WithTrailingTrivia(conditionTrailing);
        }

        var right = Parenthesize(other.WithoutTrivia(), conjunction);
        var operatorToken = SyntaxFactory.Token(
            wrapped ? WhitespaceOf(conditional.QuestionToken) : SyntaxFactory.TriviaList(SyntaxFactory.Space),
            conjunction ? SyntaxKind.AmpersandAmpersandToken : SyntaxKind.BarBarToken,
            SyntaxFactory.TriviaList(SyntaxFactory.Space));

        var replacement = SyntaxFactory.BinaryExpression(
                conjunction ? SyntaxKind.LogicalAndExpression : SyntaxKind.LogicalOrExpression,
                left,
                operatorToken,
                right)
            .WithTriviaFrom(conditional);

        return new NodeReplacement(conditional, replacement);
    }

    /// <summary>Returns whether a trivia list ends a line.</summary>
    /// <param name="trivia">The trivia to inspect.</param>
    /// <returns><see langword="true"/> when it holds a line break.</returns>
    private static bool ContainsLineBreak(in SyntaxTriviaList trivia)
    {
        foreach (var item in trivia)
        {
            if (item.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the indentation a token sits behind, dropping anything that is not whitespace.</summary>
    /// <param name="token">The conditional's <c>?</c> token.</param>
    /// <returns>The indentation to put in front of the operator.</returns>
    private static SyntaxTriviaList WhitespaceOf(SyntaxToken token)
    {
        var kept = new List<SyntaxTrivia>();
        foreach (var trivia in token.LeadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                kept.Add(trivia);
            }
        }

        return SyntaxFactory.TriviaList(kept);
    }

    /// <summary>Negates a condition for the forms whose literal branch is the true one.</summary>
    /// <param name="condition">The condition to negate.</param>
    /// <returns>The negated condition.</returns>
    /// <remarks>
    /// A leading <c>!</c> is dropped rather than doubled, a pattern test flips its own <c>not</c> rather
    /// than wearing one, and anything else that is not already a primary expression is parenthesized so
    /// the <c>!</c> binds to the whole condition.
    /// </remarks>
    private static ExpressionSyntax Negate(ExpressionSyntax condition)
    {
        var inner = ExpressionSimplificationAnalyzer.Unwrap(condition);
        if (inner is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation)
        {
            return ExpressionSimplificationAnalyzer.Unwrap(negation.Operand).WithoutTrivia();
        }

        if (SupportsNotPattern(condition))
        {
            switch (inner)
            {
                case IsPatternExpressionSyntax pattern:
                    return pattern.WithoutTrivia().WithPattern(PatternNegation.Negate(pattern.Pattern.WithoutTrivia()));

                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax type } typeTest:
                    return SyntaxFactory.IsPatternExpression(
                        typeTest.Left.WithoutTrivia(),
                        PatternNegation.Negate(SyntaxFactory.TypePattern(type.WithoutTrivia())));
            }
        }

        var operand = PrimaryExpressionClassification.IsPrimary(inner)
            ? inner.WithoutTrivia()
            : SyntaxFactory.ParenthesizedExpression(inner.WithoutTrivia());
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand);
    }

    /// <summary>Returns whether the tree was parsed at a language version that has <c>not</c> patterns.</summary>
    /// <param name="condition">The condition being negated.</param>
    /// <returns><see langword="true"/> from C# 9 on.</returns>
    private static bool SupportsNotPattern(ExpressionSyntax condition) =>
        condition.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp9 };

    /// <summary>Parenthesizes an operand whose own operator would regroup under the one being built.</summary>
    /// <param name="operand">The non-literal branch.</param>
    /// <param name="conjunction">Whether the expression being built is a conjunction.</param>
    /// <returns>The operand, parenthesized when precedence requires it.</returns>
    private static ExpressionSyntax Parenthesize(ExpressionSyntax operand, bool conjunction) =>
        conjunction && operand.IsKind(SyntaxKind.LogicalOrExpression)
            ? SyntaxFactory.ParenthesizedExpression(operand)
            : operand;
}
