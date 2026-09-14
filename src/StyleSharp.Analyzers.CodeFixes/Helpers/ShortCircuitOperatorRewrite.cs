// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared rewrite for the fixes that turn an eager boolean <c>&amp;</c> / <c>|</c> into its short-circuiting
/// <c>&amp;&amp;</c> / <c>||</c> form, keeping the operator token's trivia and re-parenthesizing when the
/// parent operator would otherwise bind tighter.
/// </summary>
internal static class ShortCircuitOperatorRewrite
{
    /// <summary>Batches the rewrite across a document for every fix that short-circuits the reported operator.</summary>
    internal static readonly BatchEditFixAllProvider FixAll = new(Find, static (current, _) => Rewrite((BinaryExpressionSyntax)current));

    /// <summary>Returns whether the resolved node is one of the eager operator kinds this rewrite handles.</summary>
    /// <param name="binary">The candidate expression.</param>
    /// <returns><see langword="true"/> for <c>&amp;</c> and <c>|</c> expressions.</returns>
    internal static bool IsFixableKind(BinaryExpressionSyntax binary) =>
        binary.RawKind is (int)SyntaxKind.BitwiseAndExpression or (int)SyntaxKind.BitwiseOrExpression;

    /// <summary>Resolves the eager boolean operator a diagnostic was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The <c>&amp;</c> or <c>|</c> expression, or <see langword="null"/> when the reported node is not one.</returns>
    internal static BinaryExpressionSyntax? Find(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BinaryExpressionSyntax binary && IsFixableKind(binary)
            ? binary
            : null;

    /// <summary>Replaces the resolved eager operator with its short-circuiting form in the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root the operator belongs to.</param>
    /// <param name="binary">The eager binary expression.</param>
    /// <returns>The updated document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax binary) =>
        TargetCodeFix.Apply(document, root, binary, Rewrite);

    /// <summary>Builds the short-circuiting replacement, preserving the operator token's trivia.</summary>
    /// <param name="binary">The eager binary expression.</param>
    /// <returns>The rewritten expression, parenthesized when the parent operator binds tighter than the conditional form.</returns>
    internal static ExpressionSyntax Rewrite(BinaryExpressionSyntax binary)
    {
        var isAnd = binary.IsKind(SyntaxKind.BitwiseAndExpression);
        var operatorToken = SyntaxFactory.Token(
            binary.OperatorToken.LeadingTrivia,
            isAnd ? SyntaxKind.AmpersandAmpersandToken : SyntaxKind.BarBarToken,
            binary.OperatorToken.TrailingTrivia);
        var replacement = SyntaxFactory.BinaryExpression(isAnd ? SyntaxKind.LogicalAndExpression : SyntaxKind.LogicalOrExpression, binary.Left, operatorToken, binary.Right);
        return NeedsParentheses(binary.Parent)
            ? SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.Token(replacement.GetLeadingTrivia(), SyntaxKind.OpenParenToken, default),
                replacement.WithoutTrivia(),
                SyntaxFactory.Token(default, SyntaxKind.CloseParenToken, replacement.GetTrailingTrivia()))
            : replacement;
    }

    /// <summary>Returns whether the parent operator binds tighter than the conditional form, so the replacement must keep its grouping.</summary>
    /// <param name="parent">The reported expression's parent.</param>
    /// <returns><see langword="true"/> when the parent is a bitwise binary operator.</returns>
    private static bool NeedsParentheses(SyntaxNode? parent) =>
        parent is BinaryExpressionSyntax
        {
            RawKind: (int)SyntaxKind.BitwiseAndExpression or (int)SyntaxKind.ExclusiveOrExpression or (int)SyntaxKind.BitwiseOrExpression,
        };
}
