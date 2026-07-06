// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves a catch block's hand-rolled rethrow condition into a <c>catch ... when</c> filter
/// (SST2009). The branch the handler actually keeps becomes the new catch body, and the filter
/// condition is negated when the winning branch was the one that did not rethrow.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2009UseExceptionFilterCodeFixProvider))]
[Shared]
public sealed class Sst2009UseExceptionFilterCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The trivia count of an indentation-plus-line-break pair forming one blank line.</summary>
    private const int WhitespaceEolPairLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseExceptionFilter.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Move the condition into a 'when' filter", nameof(Sst2009UseExceptionFilterCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the catch clause with its <c>when</c>-filtered form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="catchClause">The catch clause to rewrite.</param>
    /// <returns>The updated document, or the original document when the shape no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, CatchClauseSyntax catchClause)
        => BuildReplacement(catchClause) is { } replacement
            ? document.WithSyntaxRoot(root.ReplaceNode(catchClause, replacement))
            : document;

    /// <summary>Resolves the reported catch clause and builds its filtered form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<CatchClauseSyntax>() is { } catchClause
            && BuildReplacement(catchClause) is { } replacement
            ? new NodeReplacement(catchClause, replacement, RewriteCurrent)
            : null;

    /// <summary>Rewrites the current catch clause during batch FixAll composition.</summary>
    /// <param name="current">The current catch clause node.</param>
    /// <returns>The rewritten catch clause, or the current node when the shape no longer matches.</returns>
    private static SyntaxNode RewriteCurrent(SyntaxNode current)
        => current is CatchClauseSyntax catchClause && BuildReplacement(catchClause) is { } replacement
            ? replacement
            : current;

    /// <summary>Builds the filtered catch clause, or <see langword="null"/> when the shape no longer matches.</summary>
    /// <param name="catchClause">The catch clause to rewrite.</param>
    /// <returns>The replacement catch clause.</returns>
    private static CatchClauseSyntax? BuildReplacement(CatchClauseSyntax catchClause)
    {
        var statements = catchClause.Block.Statements;
        if (catchClause.Filter is not null
            || statements.Count == 0
            || statements[0] is not IfStatementSyntax ifStatement
            || !Sst2009UseExceptionFilterAnalyzer.MatchesFilterShape(ifStatement, statements.Count))
        {
            return null;
        }

        ExpressionSyntax condition;
        SyntaxList<StatementSyntax> newStatements;
        if (ifStatement.Else is { } elseClause)
        {
            var thenRethrows = Sst2009UseExceptionFilterAnalyzer.IsBareRethrow(ifStatement.Statement);
            condition = thenRethrows ? Negate(ifStatement.Condition) : ifStatement.Condition.WithoutTrivia();
            newStatements = BranchStatements(thenRethrows ? elseClause.Statement : ifStatement.Statement);
        }
        else
        {
            condition = Negate(ifStatement.Condition);
            newStatements = RemainingStatements(statements);
        }

        return WithFilter(catchClause, condition)
            .WithBlock(catchClause.Block.WithStatements(newStatements))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Attaches a <c>when</c> filter to the catch clause, keeping the line break before the block.</summary>
    /// <param name="catchClause">The catch clause to extend.</param>
    /// <param name="condition">The trivia-free filter condition.</param>
    /// <returns>The catch clause with the filter attached.</returns>
    private static CatchClauseSyntax WithFilter(CatchClauseSyntax catchClause, ExpressionSyntax condition)
    {
        var filter = SyntaxFactory.CatchFilterClause(condition)
            .WithWhenKeyword(SyntaxFactory.Token(SyntaxKind.WhenKeyword).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space));

        if (catchClause.Declaration is { } declaration)
        {
            var closeParen = declaration.CloseParenToken;
            return catchClause
                .WithDeclaration(declaration.WithCloseParenToken(closeParen.WithTrailingTrivia()))
                .WithFilter(filter.WithCloseParenToken(filter.CloseParenToken.WithTrailingTrivia(closeParen.TrailingTrivia)));
        }

        var catchKeyword = catchClause.CatchKeyword;
        return catchClause
            .WithCatchKeyword(catchKeyword.WithTrailingTrivia())
            .WithFilter(filter.WithCloseParenToken(filter.CloseParenToken.WithTrailingTrivia(catchKeyword.TrailingTrivia)));
    }

    /// <summary>Returns the statements a surviving branch contributes to the new catch body.</summary>
    /// <param name="branch">The surviving branch statement.</param>
    /// <returns>The branch's statements, with leading blank lines stripped from the first.</returns>
    private static SyntaxList<StatementSyntax> BranchStatements(StatementSyntax branch)
        => branch is BlockSyntax block
            ? StripLeadingBlankLines(block.Statements)
            : SyntaxFactory.SingletonList(branch);

    /// <summary>Returns the catch block's statements after the removed <c>if</c>.</summary>
    /// <param name="statements">The original catch block statements.</param>
    /// <returns>The remaining statements, with leading blank lines stripped from the first.</returns>
    private static SyntaxList<StatementSyntax> RemainingStatements(SyntaxList<StatementSyntax> statements)
    {
        var remaining = new StatementSyntax[statements.Count - 1];
        for (var i = 1; i < statements.Count; i++)
        {
            remaining[i - 1] = statements[i];
        }

        remaining[0] = StripLeadingBlankLines(remaining[0]);
        return SyntaxFactory.List(remaining);
    }

    /// <summary>Strips leading blank lines from a statement list's first statement.</summary>
    /// <param name="statements">The statement list.</param>
    /// <returns>The statement list without leading blank lines.</returns>
    private static SyntaxList<StatementSyntax> StripLeadingBlankLines(SyntaxList<StatementSyntax> statements)
        => statements.Count == 0
            ? statements
            : statements.Replace(statements[0], StripLeadingBlankLines(statements[0]));

    /// <summary>Removes blank lines at the start of a statement's leading trivia, keeping comments and indentation.</summary>
    /// <param name="statement">The statement to trim.</param>
    /// <returns>The statement without leading blank lines.</returns>
    private static StatementSyntax StripLeadingBlankLines(StatementSyntax statement)
    {
        var leading = statement.GetLeadingTrivia();
        var index = 0;
        while (index < leading.Count)
        {
            if (leading[index].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                index++;
                continue;
            }

            if (leading[index].IsKind(SyntaxKind.WhitespaceTrivia)
                && index + 1 < leading.Count
                && leading[index + 1].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                index += WhitespaceEolPairLength;
                continue;
            }

            break;
        }

        if (index == 0)
        {
            return statement;
        }

        var kept = new SyntaxTrivia[leading.Count - index];
        for (var i = index; i < leading.Count; i++)
        {
            kept[i - index] = leading[i];
        }

        return statement.WithLeadingTrivia(kept);
    }

    /// <summary>Negates a condition: unwraps a prefix <c>!</c>, inverts a comparison operator, or wraps as <c>!(condition)</c>.</summary>
    /// <param name="condition">The condition to negate.</param>
    /// <returns>The trivia-free negated condition.</returns>
    private static ExpressionSyntax Negate(ExpressionSyntax condition)
    {
        var unwrapped = Unwrap(condition);
        if (unwrapped is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } logicalNot)
        {
            return Unwrap(logicalNot.Operand).WithoutTrivia();
        }

        if (unwrapped is BinaryExpressionSyntax binary && TryInvertComparison(binary.Kind(), out var invertedKind, out var invertedToken))
        {
            var operatorToken = SyntaxFactory.Token(binary.OperatorToken.LeadingTrivia, invertedToken, binary.OperatorToken.TrailingTrivia);
            return SyntaxFactory.BinaryExpression(invertedKind, binary.Left, operatorToken, binary.Right).WithoutTrivia();
        }

        return SyntaxFactory.PrefixUnaryExpression(
            SyntaxKind.LogicalNotExpression,
            SyntaxFactory.ParenthesizedExpression(unwrapped.WithoutTrivia()));
    }

    /// <summary>Returns the inverted expression and operator-token kinds for a simple comparison.</summary>
    /// <param name="kind">The comparison expression kind.</param>
    /// <param name="invertedKind">The inverted expression kind.</param>
    /// <param name="invertedToken">The inverted operator-token kind.</param>
    /// <returns><see langword="true"/> when the kind is an invertible comparison.</returns>
    private static bool TryInvertComparison(SyntaxKind kind, out SyntaxKind invertedKind, out SyntaxKind invertedToken)
    {
        switch (kind)
        {
            case SyntaxKind.EqualsExpression:
            {
                invertedKind = SyntaxKind.NotEqualsExpression;
                invertedToken = SyntaxKind.ExclamationEqualsToken;
                return true;
            }

            case SyntaxKind.NotEqualsExpression:
            {
                invertedKind = SyntaxKind.EqualsExpression;
                invertedToken = SyntaxKind.EqualsEqualsToken;
                return true;
            }

            case SyntaxKind.LessThanExpression:
            {
                invertedKind = SyntaxKind.GreaterThanOrEqualExpression;
                invertedToken = SyntaxKind.GreaterThanEqualsToken;
                return true;
            }

            case SyntaxKind.LessThanOrEqualExpression:
            {
                invertedKind = SyntaxKind.GreaterThanExpression;
                invertedToken = SyntaxKind.GreaterThanToken;
                return true;
            }

            case SyntaxKind.GreaterThanExpression:
            {
                invertedKind = SyntaxKind.LessThanOrEqualExpression;
                invertedToken = SyntaxKind.LessThanEqualsToken;
                return true;
            }

            case SyntaxKind.GreaterThanOrEqualExpression:
            {
                invertedKind = SyntaxKind.LessThanExpression;
                invertedToken = SyntaxKind.LessThanToken;
                return true;
            }

            default:
            {
                invertedKind = default;
                invertedToken = default;
                return false;
            }
        }
    }

    /// <summary>Removes enclosing parentheses around an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The unwrapped expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
