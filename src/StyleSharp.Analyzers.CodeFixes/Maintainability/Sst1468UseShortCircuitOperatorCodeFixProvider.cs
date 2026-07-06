// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a non-short-circuiting boolean <c>&amp;</c> / <c>|</c> with <c>&amp;&amp;</c> / <c>||</c> (SST1468).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1468UseShortCircuitOperatorCodeFixProvider))]
[Shared]
public sealed class Sst1468UseShortCircuitOperatorCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UseShortCircuitOperator.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax binary || !IsFixableKind(binary))
            {
                continue;
            }

            var title = binary.IsKind(SyntaxKind.BitwiseAndExpression) ? "Use '&&'" : "Use '||'";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(Apply(context.Document, root, binary)),
                    equivalenceKey: nameof(Sst1468UseShortCircuitOperatorCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax binary || !IsFixableKind(binary))
        {
            return;
        }

        editor.ReplaceNode(binary, (current, _) => Rewrite((BinaryExpressionSyntax)current));
    }

    /// <summary>Replaces the eager operator with its short-circuiting form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="binary">The reported <c>&amp;</c> / <c>|</c> expression.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax binary)
        => document.WithSyntaxRoot(root.ReplaceNode(binary, Rewrite(binary)));

    /// <summary>Returns whether the resolved node is one of the reported operator kinds.</summary>
    /// <param name="binary">The candidate expression.</param>
    /// <returns><see langword="true"/> for <c>&amp;</c> and <c>|</c> expressions.</returns>
    private static bool IsFixableKind(BinaryExpressionSyntax binary)
        => binary.RawKind is (int)SyntaxKind.BitwiseAndExpression or (int)SyntaxKind.BitwiseOrExpression;

    /// <summary>Builds the short-circuiting replacement, preserving the operator token's trivia.</summary>
    /// <param name="binary">The eager binary expression.</param>
    /// <returns>The rewritten expression, parenthesized when the parent operator binds tighter than the conditional form.</returns>
    private static ExpressionSyntax Rewrite(BinaryExpressionSyntax binary)
    {
        var isAnd = binary.IsKind(SyntaxKind.BitwiseAndExpression);
        var operatorToken = SyntaxFactory.Token(
            binary.OperatorToken.LeadingTrivia,
            isAnd ? SyntaxKind.AmpersandAmpersandToken : SyntaxKind.BarBarToken,
            binary.OperatorToken.TrailingTrivia);
        var replacement = SyntaxFactory.BinaryExpression(isAnd ? SyntaxKind.LogicalAndExpression : SyntaxKind.LogicalOrExpression, binary.Left, operatorToken, binary.Right);
        return NeedsParentheses(binary.Parent)
            ? SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia()).WithTriviaFrom(replacement)
            : replacement;
    }

    /// <summary>Returns whether the parent operator binds tighter than the conditional form, so the replacement must keep its grouping.</summary>
    /// <param name="parent">The reported expression's parent.</param>
    /// <returns><see langword="true"/> when the parent is a bitwise binary operator.</returns>
    private static bool NeedsParentheses(SyntaxNode? parent)
        => parent is BinaryExpressionSyntax
        {
            RawKind: (int)SyntaxKind.BitwiseAndExpression or (int)SyntaxKind.ExclusiveOrExpression or (int)SyntaxKind.BitwiseOrExpression,
        };
}
