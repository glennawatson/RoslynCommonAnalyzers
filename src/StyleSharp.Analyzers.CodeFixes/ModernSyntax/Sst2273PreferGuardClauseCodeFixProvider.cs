// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Inverts a trailing wrapping <c>if</c> into an early-exit guard (SST2273): the condition is negated to head
/// a <c>if (!cond) return;</c> (or <c>continue;</c> inside a loop), and the previously wrapped work is lifted
/// to the outer block. The rewritten block is formatter-annotated so the lifted work is re-indented.
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
        => ReplaceNodeCodeFix.RegisterAsync(context, "Convert to an early-exit guard clause", nameof(Sst2273PreferGuardClauseCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported <c>if</c> and rewrites its block with the guard and the lifted work.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The block replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement
            || ifStatement.Parent is not BlockSyntax block
            || !Sst2273PreferGuardClauseAnalyzer.TryGetGuard(ifStatement, out var jumpKind))
        {
            return null;
        }

        var index = block.Statements.IndexOf(ifStatement);
        var work = ifStatement.Statement is BlockSyntax body
            ? body.Statements
            : SyntaxFactory.SingletonList(ifStatement.Statement);

        var guard = BuildGuard(ifStatement, jumpKind);
        var statements = block.Statements
            .RemoveAt(index)
            .Insert(index, guard)
            .InsertRange(index + 1, work);
        var newBlock = block.WithStatements(statements).WithAdditionalAnnotations(Formatter.Annotation);
        return new NodeReplacement(block, newBlock);
    }

    /// <summary>Builds the guard <c>if (!cond) return;</c> / <c>if (!cond) continue;</c>.</summary>
    /// <param name="ifStatement">The original trailing <c>if</c>, used for its leading trivia.</param>
    /// <param name="jumpKind">The jump kind for the guard.</param>
    /// <returns>The formatter-friendly guard statement.</returns>
    private static IfStatementSyntax BuildGuard(IfStatementSyntax ifStatement, SyntaxKind jumpKind)
    {
        StatementSyntax jump = jumpKind == SyntaxKind.ContinueStatement
            ? SyntaxFactory.ContinueStatement()
            : SyntaxFactory.ReturnStatement();
        return SyntaxFactory.IfStatement(Negate(ifStatement.Condition), jump)
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia());
    }

    /// <summary>Negates a condition: strips a leading <c>!</c>, flips an equality comparison, else wraps <c>!(condition)</c>.</summary>
    /// <param name="condition">The condition to negate.</param>
    /// <returns>The trivia-free negated condition.</returns>
    /// <remarks>
    /// Equality is flipped because <c>!(a == b)</c> and <c>a != b</c> agree even for NaN. A relational or
    /// combined condition is wrapped in <c>!(...)</c> rather than flipped, because inverting <c>&lt;</c> to
    /// <c>&gt;=</c> would disagree on NaN operands.
    /// </remarks>
    private static ExpressionSyntax Negate(ExpressionSyntax condition)
    {
        var inner = ExpressionSimplificationAnalyzer.Unwrap(condition);

        if (inner is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation)
        {
            return ExpressionSimplificationAnalyzer.Unwrap(negation.Operand).WithoutTrivia();
        }

        if (inner is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } equals)
        {
            return SyntaxFactory.BinaryExpression(
                SyntaxKind.NotEqualsExpression,
                equals.Left.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.ExclamationEqualsToken),
                equals.Right.WithoutTrivia());
        }

        if (inner is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression } notEquals)
        {
            return SyntaxFactory.BinaryExpression(
                SyntaxKind.EqualsExpression,
                notEquals.Left.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.EqualsEqualsToken),
                notEquals.Right.WithoutTrivia());
        }

        var operand = PrimaryExpressionClassification.IsPrimary(inner)
            ? inner.WithoutTrivia()
            : SyntaxFactory.ParenthesizedExpression(inner.WithoutTrivia());
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand);
    }
}
