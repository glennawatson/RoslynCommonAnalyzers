// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>Merges an <c>if</c> that only wraps another <c>if</c> into one condition joined by <c>&amp;&amp;</c> (SST2013).</summary>
/// <remarks>
/// <para>
/// Either operand is parenthesized when it binds looser than <c>&amp;&amp;</c> — an <c>||</c>, a <c>??</c>, a
/// <c>?:</c>, an assignment or a <c>switch</c> expression — because dropping those parentheses would silently
/// regroup the condition. Everything that binds tighter (<c>==</c>, <c>is</c>, <c>&amp;</c>, <c>|</c>) is left
/// exactly as it was written.
/// </para>
/// <para>
/// The inner statement is carried over whole, and any comment that lived in the discarded scaffolding — after
/// the outer condition, around the braces, before the inner <c>if</c> — is hoisted above the merged statement
/// rather than deleted. A comment is somebody's explanation; a code fix does not get to decide it was wrong.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2013MergeNestedIfCodeFixProvider))]
[Shared]
public sealed class Sst2013MergeNestedIfCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.MergeNestedIf.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetPair(root, diagnostic, out var outer, out var inner))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Merge the conditions",
                    _ => Task.FromResult(Apply(context.Document, root, outer!, inner!)),
                    equivalenceKey: nameof(Sst2013MergeNestedIfCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetPair(editor.OriginalRoot, diagnostic, out var outer, out var inner))
        {
            return;
        }

        editor.ReplaceNode(outer!, Merge(outer!, inner!));
    }

    /// <summary>Merges one reported nested pair.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="outer">The outer if statement.</param>
    /// <param name="inner">The inner if statement.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, IfStatementSyntax outer, IfStatementSyntax inner)
        => document.WithSyntaxRoot(root.ReplaceNode(outer, Merge(outer, inner)));

    /// <summary>Resolves the reported outer <c>if</c> and the inner one it wraps.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="outer">The outer if statement, when the shape still matches.</param>
    /// <param name="inner">The inner if statement, when the shape still matches.</param>
    /// <returns><see langword="true"/> when the pair can still be merged.</returns>
    private static bool TryGetPair(
        SyntaxNode root,
        Diagnostic diagnostic,
        out IfStatementSyntax? outer,
        out IfStatementSyntax? inner)
    {
        inner = null;
        outer = root.FindNode(diagnostic.Location.SourceSpan) as IfStatementSyntax;
        if (outer is null)
        {
            return false;
        }

        inner = Sst2013MergeNestedIfAnalyzer.GetMergeableInnerIf(outer);
        return inner is not null;
    }

    /// <summary>Builds the merged <c>if</c> from a nested pair.</summary>
    /// <param name="outer">The outer if statement.</param>
    /// <param name="inner">The inner if statement.</param>
    /// <returns>The merged statement, annotated for reformatting.</returns>
    private static IfStatementSyntax Merge(IfStatementSyntax outer, IfStatementSyntax inner)
    {
        var condition = Join(Parenthesize(outer.Condition), Parenthesize(inner.Condition));
        var merged = outer
            .WithCondition(condition)
            .WithStatement(inner.Statement)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var carried = CollectDiscardedComments(outer, inner);
        return carried.Count == 0 ? merged : merged.WithLeadingTrivia(BuildLeadingTrivia(outer, carried));
    }

    /// <summary>Joins two conditions with <c>&amp;&amp;</c>, left-associatively.</summary>
    /// <param name="left">The outer condition.</param>
    /// <param name="right">The inner condition.</param>
    /// <returns>The joined condition.</returns>
    /// <remarks>
    /// <c>&amp;&amp;</c> is left-associative, so an inner condition that is itself a conjunction has to be
    /// folded into the left spine rather than hung off the right of a new operator. The text would read the
    /// same either way, but the tree would not match the one the compiler builds from that text.
    /// </remarks>
    private static BinaryExpressionSyntax Join(ExpressionSyntax left, ExpressionSyntax right)
    {
        if (right is BinaryExpressionSyntax conjunction && conjunction.IsKind(SyntaxKind.LogicalAndExpression))
        {
            return SyntaxFactory.BinaryExpression(
                SyntaxKind.LogicalAndExpression,
                Join(left, conjunction.Left),
                conjunction.OperatorToken,
                conjunction.Right);
        }

        var operatorToken = SyntaxFactory.Token(SyntaxKind.AmpersandAmpersandToken)
            .WithLeadingTrivia(SyntaxFactory.Space)
            .WithTrailingTrivia(SyntaxFactory.Space);
        return SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, left, operatorToken, right);
    }

    /// <summary>Wraps an operand in parentheses when it binds looser than <c>&amp;&amp;</c>.</summary>
    /// <param name="expression">The condition operand.</param>
    /// <returns>The operand, parenthesized only where the grouping would otherwise change.</returns>
    private static ExpressionSyntax Parenthesize(ExpressionSyntax expression)
        => NeedsParentheses(expression)
            ? SyntaxFactory.ParenthesizedExpression(expression.WithoutTrivia()).WithTriviaFrom(expression)
            : expression;

    /// <summary>Returns whether an expression binds looser than <c>&amp;&amp;</c>.</summary>
    /// <param name="expression">The condition operand.</param>
    /// <returns><see langword="true"/> when omitting parentheses would regroup the condition.</returns>
    private static bool NeedsParentheses(ExpressionSyntax expression) => expression switch
    {
        AssignmentExpressionSyntax => true,
        ConditionalExpressionSyntax => true,
        SwitchExpressionSyntax => true,
        BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.LogicalOrExpression) || binary.IsKind(SyntaxKind.CoalesceExpression),
        _ => false,
    };

    /// <summary>Collects the comments that live in the scaffolding the merge removes.</summary>
    /// <param name="outer">The outer if statement.</param>
    /// <param name="inner">The inner if statement.</param>
    /// <returns>The comments that would otherwise be dropped, in source order.</returns>
    private static List<SyntaxTrivia> CollectDiscardedComments(IfStatementSyntax outer, IfStatementSyntax inner)
    {
        var comments = new List<SyntaxTrivia>(2);
        AddComments(comments, outer.CloseParenToken.TrailingTrivia);
        AddComments(comments, outer.Statement.GetLeadingTrivia());

        if (outer.Statement is BlockSyntax block)
        {
            AddComments(comments, block.OpenBraceToken.TrailingTrivia);
            AddComments(comments, inner.GetLeadingTrivia());
            AddComments(comments, block.CloseBraceToken.LeadingTrivia);
            AddComments(comments, block.CloseBraceToken.TrailingTrivia);
        }

        AddComments(comments, inner.CloseParenToken.TrailingTrivia);
        return comments;
    }

    /// <summary>Appends every comment in a trivia list to the running collection.</summary>
    /// <param name="comments">The running collection.</param>
    /// <param name="trivia">The trivia to scan.</param>
    private static void AddComments(List<SyntaxTrivia> comments, SyntaxTriviaList trivia)
    {
        for (var i = 0; i < trivia.Count; i++)
        {
            var candidate = trivia[i];
            if (candidate.IsKind(SyntaxKind.SingleLineCommentTrivia) || candidate.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                comments.Add(candidate);
            }
        }
    }

    /// <summary>Builds the merged statement's leading trivia, carrying the rescued comments above it.</summary>
    /// <param name="outer">The outer if statement, whose own leading trivia is kept.</param>
    /// <param name="carried">The comments rescued from the discarded scaffolding.</param>
    /// <returns>The leading trivia for the merged statement.</returns>
    private static SyntaxTriviaList BuildLeadingTrivia(IfStatementSyntax outer, List<SyntaxTrivia> carried)
    {
        var leading = new List<SyntaxTrivia>(outer.GetLeadingTrivia());
        for (var i = 0; i < carried.Count; i++)
        {
            leading.Add(carried[i]);
            leading.Add(SyntaxFactory.ElasticCarriageReturnLineFeed);
        }

        return SyntaxFactory.TriviaList(leading);
    }
}
