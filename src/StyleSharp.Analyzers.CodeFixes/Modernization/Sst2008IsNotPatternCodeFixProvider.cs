// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a negated pattern test as an <c>is not</c> pattern (SST2008). A pattern that is already
/// negated loses its <c>not</c> rather than gaining a second one, and the grouping the <c>!</c> needed
/// goes with it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2008IsNotPatternCodeFixProvider))]
[Shared]
public sealed class Sst2008IsNotPatternCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseIsNotPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Use an 'is not' pattern", nameof(Sst2008IsNotPatternCodeFixProvider), CanRewrite, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpression
            && ExpressionShapes.WalkDownParentheses(notExpression.Operand) is IsPatternExpressionSyntax;

    /// <summary>Resolves the reported negation and builds its <c>is not</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpression
            && ExpressionShapes.WalkDownParentheses(notExpression.Operand) is IsPatternExpressionSyntax isPattern
            ? new NodeReplacement(
                notExpression,
                isPattern.Update(
                    isPattern.Expression.WithLeadingTrivia(notExpression.GetLeadingTrivia()),
                    isPattern.IsKeyword,
                    PatternNegation.Negate(isPattern.Pattern.WithoutLeadingTrivia()).WithTrailingTrivia(notExpression.GetTrailingTrivia())))
            : null;
}
