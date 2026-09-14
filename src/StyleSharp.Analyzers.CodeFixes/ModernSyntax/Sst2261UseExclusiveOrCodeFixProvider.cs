// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an exclusive-or reimplementation as <c>x ^ y</c> (SST2261). The two operands are the side-effect-free
/// values from the original conjunctions, and the whole expression's trivia carries through.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2261UseExclusiveOrCodeFixProvider))]
[Shared]
public sealed class Sst2261UseExclusiveOrCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseExclusiveOr.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Use the exclusive-or operator", nameof(Sst2261UseExclusiveOrCodeFixProvider), CanRewrite, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        EnclosingBinaryExpression.Find(root, diagnostic, SyntaxKind.LogicalOrExpression, SyntaxKind.BitwiseOrExpression) is { } binary
            && Sst2261UseExclusiveOrAnalyzer.TryMatch(binary, out var _, out var _);

    /// <summary>Resolves the reported disjunction and rewrites it to an exclusive-or.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        EnclosingBinaryExpression.Find(root, diagnostic, SyntaxKind.LogicalOrExpression, SyntaxKind.BitwiseOrExpression) is { } binary
            && Sst2261UseExclusiveOrAnalyzer.TryMatch(binary, out var x, out var y)
            ? new NodeReplacement(binary, Build(binary, x, y))
            : null;

    /// <summary>Builds the <c>x ^ y</c> expression replacing the reported expression.</summary>
    /// <param name="binary">The reported expression, used for its trivia.</param>
    /// <param name="x">The first exclusive-or operand.</param>
    /// <param name="y">The second exclusive-or operand.</param>
    /// <returns>The replacement expression.</returns>
    private static BinaryExpressionSyntax Build(BinaryExpressionSyntax binary, ExpressionSyntax x, ExpressionSyntax y)
    {
        var caret = SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.CaretToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        return SyntaxFactory.BinaryExpression(
            SyntaxKind.ExclusiveOrExpression,
            x.WithLeadingTrivia(binary.GetLeadingTrivia()).WithoutTrailingTrivia(),
            caret,
            y.WithoutLeadingTrivia().WithTrailingTrivia(binary.GetTrailingTrivia()));
    }
}
