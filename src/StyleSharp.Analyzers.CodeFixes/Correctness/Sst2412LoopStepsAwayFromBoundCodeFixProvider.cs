// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flips the comparison of a for loop that steps its counter the wrong way (SST2412), so the loop runs
/// toward its bound instead of away from it.
/// </summary>
/// <remarks>
/// The fix negates the relational operator in place — <c>&lt;</c> becomes <c>&gt;=</c>, <c>&gt;</c> becomes
/// <c>&lt;=</c>, and so on — which turns <c>for (i = len - 1; i &lt; 0; i--)</c> into the intended
/// <c>i &gt;= 0</c>. Both operands and their trivia are kept exactly where they were; only the operator
/// token changes. It is offered as a suggestion the author confirms, because the other repair — reversing
/// the step — is equally valid and only they know which was meant.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2412LoopStepsAwayFromBoundCodeFixProvider))]
[Shared]
public sealed class Sst2412LoopStepsAwayFromBoundCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.LoopStepsAwayFromBound.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Flip the comparison so the loop steps toward the bound",
            nameof(Sst2412LoopStepsAwayFromBoundCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported comparison and negates its operator.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax comparison
            || Negate(comparison.Kind()) is not { } negated)
        {
            return null;
        }

        return new NodeReplacement(comparison, Flip(comparison, negated), current => Rewrite(current, negated));
    }

    /// <summary>Re-applies the flip after any nested batch edit.</summary>
    /// <param name="current">The current node.</param>
    /// <param name="negated">The negated comparison kind.</param>
    /// <returns>The flipped comparison, or the node unchanged.</returns>
    private static SyntaxNode Rewrite(SyntaxNode current, SyntaxKind negated)
        => current is BinaryExpressionSyntax comparison ? Flip(comparison, negated) : current;

    /// <summary>Builds the flipped comparison, keeping both operands and the operator's trivia.</summary>
    /// <param name="comparison">The original comparison.</param>
    /// <param name="negated">The negated comparison kind.</param>
    /// <returns>The flipped comparison.</returns>
    private static BinaryExpressionSyntax Flip(BinaryExpressionSyntax comparison, SyntaxKind negated)
    {
        var operatorToken = SyntaxFactory.Token(
            comparison.OperatorToken.LeadingTrivia,
            OperatorToken(negated),
            comparison.OperatorToken.TrailingTrivia);
        return SyntaxFactory.BinaryExpression(negated, comparison.Left, operatorToken, comparison.Right);
    }

    /// <summary>Negates a relational comparison kind.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <returns>The negated kind, or <see langword="null"/> when the kind is not relational.</returns>
    private static SyntaxKind? Negate(SyntaxKind kind) => kind switch
    {
        SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanExpression,
        SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanExpression,
        _ => null,
    };

    /// <summary>Gets the operator token kind for a comparison kind.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <returns>The operator token kind.</returns>
    private static SyntaxKind OperatorToken(SyntaxKind kind) => kind switch
    {
        SyntaxKind.LessThanExpression => SyntaxKind.LessThanToken,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.LessThanEqualsToken,
        SyntaxKind.GreaterThanExpression => SyntaxKind.GreaterThanToken,
        _ => SyntaxKind.GreaterThanEqualsToken,
    };
}
