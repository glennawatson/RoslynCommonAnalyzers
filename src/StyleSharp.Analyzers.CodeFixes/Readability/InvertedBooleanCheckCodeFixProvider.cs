// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an inverted comparison (SST1172) to use the opposite operator, dropping the <c>!</c>.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InvertedBooleanCheckCodeFixProvider))]
[Shared]
public sealed class InvertedBooleanCheckCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoInvertedBooleanCheck.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not PrefixUnaryExpressionSyntax not
                || ExpressionSimplificationAnalyzer.Unwrap(not.Operand) is not BinaryExpressionSyntax binary
                || !ExpressionSimplificationAnalyzer.TryGetOpposite(binary.Kind(), out _, out _, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the opposite operator",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, not, binary)),
                    equivalenceKey: nameof(InvertedBooleanCheckCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not PrefixUnaryExpressionSyntax not
            || ExpressionSimplificationAnalyzer.Unwrap(not.Operand) is not BinaryExpressionSyntax binary
            || !ExpressionSimplificationAnalyzer.TryGetOpposite(binary.Kind(), out var expressionKind, out var tokenKind, out _))
        {
            return;
        }

        var left = binary.Left.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space);
        var operatorToken = SyntaxFactory.Token(tokenKind).WithTrailingTrivia(SyntaxFactory.Space);
        var right = binary.Right.WithoutTrivia();
        editor.ReplaceNode(not, SyntaxFactory.BinaryExpression(expressionKind, left, operatorToken, right).WithTriviaFrom(not));
    }

    /// <summary>Replaces the inverted comparison with the opposite-operator form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="not">The logical-not expression to rewrite.</param>
    /// <param name="binary">The comparison wrapped by the logical-not.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, PrefixUnaryExpressionSyntax not, BinaryExpressionSyntax binary)
    {
        ExpressionSimplificationAnalyzer.TryGetOpposite(binary.Kind(), out var expressionKind, out var tokenKind, out _);

        var left = binary.Left.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space);
        var operatorToken = SyntaxFactory.Token(tokenKind).WithTrailingTrivia(SyntaxFactory.Space);
        var right = binary.Right.WithoutTrivia();
        var replacement = SyntaxFactory.BinaryExpression(expressionKind, left, operatorToken, right).WithTriviaFrom(not);

        return document.WithSyntaxRoot(root.ReplaceNode(not, replacement));
    }
}
