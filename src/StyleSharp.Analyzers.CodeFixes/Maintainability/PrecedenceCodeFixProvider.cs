// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Wraps an expression in parentheses to make its precedence explicit (SST1407, SST1408, SST1418).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrecedenceCodeFixProvider))]
[Shared]
public sealed class PrecedenceCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.ArithmeticPrecedence.Id,
        MaintainabilityRules.ConditionalPrecedence.Id,
        MaintainabilityRules.NullCoalescingPrecedence.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not ExpressionSyntax expression)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add parentheses",
                    _ => AddParenthesesAsync(context.Document, root, expression),
                    equivalenceKey: nameof(PrecedenceCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not ExpressionSyntax expression)
        {
            return;
        }

        editor.ReplaceNode(expression, static (current, _) => SyntaxFactory.ParenthesizedExpression(
            SyntaxFactory.Token(current.GetLeadingTrivia(), SyntaxKind.OpenParenToken, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
            ((ExpressionSyntax)current).WithoutTrivia(),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, current.GetTrailingTrivia())));
    }

    /// <summary>Replaces the expression with a parenthesized copy that keeps its surrounding trivia.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="expression">The expression to parenthesize.</param>
    /// <returns>The updated document.</returns>
    /// <remarks>The rewrite is a pure syntax edit over a root the caller already has, so there is nothing to cancel.</remarks>
    internal static Task<Document> AddParenthesesAsync(Document document, SyntaxNode root, ExpressionSyntax expression)
    {
        var parenthesized = SyntaxFactory.ParenthesizedExpression(
            SyntaxFactory.Token(expression.GetLeadingTrivia(), SyntaxKind.OpenParenToken, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
            expression.WithoutTrivia(),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, expression.GetTrailingTrivia()));
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(expression, parenthesized)));
    }

    /// <summary>Replaces the expression with a parenthesized copy that keeps its surrounding trivia.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="expression">The expression to parenthesize.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> AddParenthesesAsync(Document document, ExpressionSyntax expression, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return await AddParenthesesAsync(document, root!, expression).ConfigureAwait(false);
    }
}
