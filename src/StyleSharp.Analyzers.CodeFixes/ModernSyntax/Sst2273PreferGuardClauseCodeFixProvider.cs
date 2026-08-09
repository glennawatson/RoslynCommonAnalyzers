// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Inverts a trailing wrapping <c>if</c> into an early-exit guard (SST2273): the condition is negated to head
/// a <c>if (!cond) { return; }</c> (or <c>continue;</c> inside a loop), and the previously wrapped work is
/// lifted to the outer block. The rewritten block is formatter-annotated so the lifted work is re-indented.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2273PreferGuardClauseCodeFixProvider))]
[Shared]
public sealed class Sst2273PreferGuardClauseCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.PreferGuardClause.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Convert to an early-exit guard clause",
            nameof(Sst2273PreferGuardClauseCodeFixProvider),
            (ReplaceNodeCodeFix.SemanticRewriter)TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, (ReplaceNodeCodeFix.SemanticRewriter)TryRewrite);

    /// <summary>Resolves the reported <c>if</c> and rewrites its block with the guard and the lifted work.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model, used to decide whether a relational operator can be flipped.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The block replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement
            || ifStatement.Parent is not BlockSyntax block
            || !Sst2273PreferGuardClauseAnalyzer.TryGetGuard(ifStatement, out var jumpKind))
        {
            return null;
        }

        var guard = BuildGuard(ifStatement, jumpKind, model);

        // The rewrite is only correct if the guard tests the opposite of what the 'if' tested. Every branch of
        // Negate produces that by construction, so a guard that came back equivalent to the original condition
        // means the negation did not happen — and an inverted guard compiles clean and silently reverses which
        // work runs. Decline rather than emit one.
        if (SyntaxFactory.AreEquivalent(guard.Condition, Unwrap(ifStatement.Condition), topLevel: false))
        {
            return null;
        }

        var index = block.Statements.IndexOf(ifStatement);
        var work = ifStatement.Statement is BlockSyntax body
            ? body.Statements
            : SyntaxFactory.SingletonList(ifStatement.Statement);

        var statements = block.Statements
            .RemoveAt(index)
            .Insert(index, guard)
            .InsertRange(index + 1, work);
        var newBlock = block.WithStatements(statements).WithAdditionalAnnotations(Formatter.Annotation);
        return new NodeReplacement(block, newBlock);
    }

    /// <summary>Builds the guard <c>if (!cond) { return; }</c> / <c>if (!cond) { continue; }</c>.</summary>
    /// <param name="ifStatement">The original trailing <c>if</c>, used for its leading trivia.</param>
    /// <param name="jumpKind">The jump kind for the guard.</param>
    /// <param name="model">The semantic model.</param>
    /// <returns>The formatter-friendly guard statement.</returns>
    /// <remarks>
    /// The jump is braced, and the guard carries a trailing line break, so the result satisfies the brace and
    /// blank-line rules a project may have enabled rather than trading one diagnostic for two.
    /// </remarks>
    private static IfStatementSyntax BuildGuard(IfStatementSyntax ifStatement, SyntaxKind jumpKind, SemanticModel model)
    {
        StatementSyntax jump = jumpKind == SyntaxKind.ContinueStatement
            ? SyntaxFactory.ContinueStatement()
            : SyntaxFactory.ReturnStatement();
        return SyntaxFactory.IfStatement(Negate(ifStatement.Condition, model), SyntaxFactory.Block(jump))
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
    }

    /// <summary>Negates a condition, pushing the negation inward rather than wrapping the whole thing.</summary>
    /// <param name="condition">The condition to negate.</param>
    /// <param name="model">The semantic model.</param>
    /// <returns>The trivia-free negated condition.</returns>
    /// <remarks>
    /// <para>
    /// A leading <c>!</c> is dropped, a pattern gains or loses its <c>not</c>, a comparison flips its
    /// operator, and <c>&amp;&amp;</c>/<c>||</c> distribute by De Morgan's laws — which preserve short-circuit
    /// order, since <c>!a || !b</c> evaluates <c>b</c> in exactly the cases <c>a &amp;&amp; b</c> did.
    /// </para>
    /// <para>
    /// A relational operator is only flipped when neither operand can be null or NaN: <c>!(a &lt; b)</c> is
    /// true for a NaN operand where <c>a &gt;= b</c> is false. Equality flips unconditionally, because
    /// <c>!(a == b)</c> and <c>a != b</c> agree even there. Anything left over is wrapped in <c>!(...)</c>.
    /// </para>
    /// </remarks>
    private static ExpressionSyntax Negate(ExpressionSyntax condition, SemanticModel model)
    {
        var inner = Unwrap(condition);

        if (inner is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation)
        {
            return Unwrap(negation.Operand).WithoutTrivia();
        }

        if (inner is IsPatternExpressionSyntax pattern && TryNegatePattern(pattern) is { } negatedPattern)
        {
            return negatedPattern;
        }

        if (inner is BinaryExpressionSyntax binary)
        {
            if (binary.IsKind(SyntaxKind.IsExpression) && TryNegateTypeCheck(binary) is { } negatedTypeCheck)
            {
                return negatedTypeCheck;
            }

            if (TryApplyDeMorgan(binary, model) is { } distributed)
            {
                return distributed;
            }

            if (TryFlipComparison(binary, model) is { } flipped)
            {
                return flipped;
            }
        }

        var operand = PrimaryExpressionClassification.IsPrimary(inner)
            ? inner.WithoutTrivia()
            : SyntaxFactory.ParenthesizedExpression(inner.WithoutTrivia());
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand);
    }

    /// <summary>Distributes a negation over <c>&amp;&amp;</c> or <c>||</c> by De Morgan's laws.</summary>
    /// <param name="binary">The logical expression to negate.</param>
    /// <param name="model">The semantic model.</param>
    /// <returns>The distributed expression, or <see langword="null"/> when the operator is not a conjunction or disjunction.</returns>
    /// <remarks>
    /// Each operand is negated in turn, so <c>!(a &amp;&amp; b &gt; 0)</c> becomes <c>!a || b &lt;= 0</c>
    /// rather than stopping at one level. An operand that comes back as the opposite logical operator is
    /// parenthesized, because <c>&amp;&amp;</c> binds tighter than <c>||</c> and would otherwise regroup.
    /// </remarks>
    private static BinaryExpressionSyntax? TryApplyDeMorgan(BinaryExpressionSyntax binary, SemanticModel model)
    {
        var (resultKind, tokenKind) = binary.Kind() switch
        {
            SyntaxKind.LogicalAndExpression => (SyntaxKind.LogicalOrExpression, SyntaxKind.BarBarToken),
            SyntaxKind.LogicalOrExpression => (SyntaxKind.LogicalAndExpression, SyntaxKind.AmpersandAmpersandToken),
            _ => (SyntaxKind.None, SyntaxKind.None),
        };

        if (resultKind == SyntaxKind.None)
        {
            return null;
        }

        return SyntaxFactory.BinaryExpression(
            resultKind,
            Regroup(Negate(binary.Left, model), resultKind),
            SyntaxFactory.Token(tokenKind).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
            Regroup(Negate(binary.Right, model), resultKind));
    }

    /// <summary>Parenthesizes an operand whose own operator would regroup inside the one being built.</summary>
    /// <param name="operand">The negated operand.</param>
    /// <param name="outerKind">The logical operator the operand is being placed under.</param>
    /// <returns>The operand, parenthesized when precedence requires it.</returns>
    private static ExpressionSyntax Regroup(ExpressionSyntax operand, SyntaxKind outerKind)
        => outerKind == SyntaxKind.LogicalAndExpression && operand.IsKind(SyntaxKind.LogicalOrExpression)
            ? SyntaxFactory.ParenthesizedExpression(operand)
            : operand;

    /// <summary>Flips a comparison to its opposite operator, when that is exactly the negation.</summary>
    /// <param name="binary">The comparison to flip.</param>
    /// <param name="model">The semantic model.</param>
    /// <returns>The flipped comparison, or <see langword="null"/> when flipping would not preserve meaning.</returns>
    private static BinaryExpressionSyntax? TryFlipComparison(BinaryExpressionSyntax binary, SemanticModel model)
    {
        if (!ExpressionSimplificationAnalyzer.TryGetOpposite(binary.Kind(), out var expressionKind, out var tokenKind, out _))
        {
            return null;
        }

        // '!(a < b)' is true when an operand is NaN and 'a >= b' is false, so a relational flip needs both
        // operands to be values that can be neither null nor NaN. Equality agrees either way.
        if (ExpressionSimplificationAnalyzer.IsRelational(binary.Kind())
            && (ExpressionSimplificationAnalyzer.IsUnsafeRelationalOperand(binary.Left, model, CancellationToken.None)
                || ExpressionSimplificationAnalyzer.IsUnsafeRelationalOperand(binary.Right, model, CancellationToken.None)))
        {
            return null;
        }

        return SyntaxFactory.BinaryExpression(
            expressionKind,
            binary.Left.WithoutTrivia(),
            SyntaxFactory.Token(tokenKind).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
            binary.Right.WithoutTrivia());
    }

    /// <summary>Negates an <c>is</c> pattern by adding or removing its <c>not</c>, when that is legal.</summary>
    /// <param name="expression">The pattern expression to negate.</param>
    /// <returns>The negated pattern, or <see langword="null"/> when it must be wrapped in <c>!</c> instead.</returns>
    /// <remarks>
    /// <c>x is not null</c> reads better negated as <c>x is null</c> than as <c>!(x is not null)</c>. Adding a
    /// <c>not</c> is only legal for a pattern that binds nothing — a <c>not</c> pattern cannot declare a
    /// variable — so a declaration or recursive pattern falls through to the <c>!</c> wrapper.
    /// </remarks>
    private static IsPatternExpressionSyntax? TryNegatePattern(IsPatternExpressionSyntax expression) => expression.Pattern switch
    {
        UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } negated
            => expression.WithPattern(negated.Pattern.WithoutTrivia()).WithoutTrivia(),
        ConstantPatternSyntax or TypePatternSyntax
            => expression.WithPattern(SyntaxFactory.UnaryPattern(expression.Pattern.WithoutTrivia())).WithoutTrivia(),
        _ => null,
    };

    /// <summary>Negates a classic <c>x is T</c> type check as the <c>x is not T</c> pattern.</summary>
    /// <param name="typeCheck">The <c>is</c> type check.</param>
    /// <returns>The negated pattern, or <see langword="null"/> below C# 9, where <c>not</c> patterns do not exist.</returns>
    private static IsPatternExpressionSyntax? TryNegateTypeCheck(BinaryExpressionSyntax typeCheck)
    {
        if (typeCheck.Right is not TypeSyntax type
            || typeCheck.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp9 })
        {
            return null;
        }

        return SyntaxFactory.IsPatternExpression(
            typeCheck.Left.WithoutTrivia(),
            SyntaxFactory.UnaryPattern(SyntaxFactory.TypePattern(type.WithoutTrivia())));
    }

    /// <summary>Strips enclosing parentheses to reach the inner expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
        => ExpressionSimplificationAnalyzer.Unwrap(expression);
}
