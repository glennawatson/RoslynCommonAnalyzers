// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a comparison that a count can never fail with the constant it always evaluates to (SST1479).</summary>
/// <remarks>
/// The fix stops at the comparison. Collapsing the <c>if</c> that wraps it is a judgement about what the
/// author meant — the guard is usually salvageable, not deletable — so the constant is left in place for them
/// to read and finish.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1479MeaninglessCountComparisonCodeFixProvider))]
[Shared]
public sealed class Sst1479MeaninglessCountComparisonCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindFoldingComparison, static (current, _) => FoldCurrent(current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.MeaninglessCountComparison.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static _ => nameof(Sst1479MeaninglessCountComparisonCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported comparison and replaces it with the constant it always evaluates to.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the comparison no longer folds.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetComparison(root, diagnostic, out var binary, out var result)
            ? new NodeReplacement(binary!, BuildLiteral(binary!, result))
            : null;

    /// <summary>Resolves the diagnostic's span back to the comparison, and re-derives its constant.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="binary">The reported comparison when found.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <returns><see langword="true"/> when the reported shape still folds.</returns>
    /// <remarks>The fold reads only the operator and the literal's sign, so the fix confirms it without binding.</remarks>
    private static bool TryGetComparison(SyntaxNode root, Diagnostic diagnostic, out BinaryExpressionSyntax? binary, out bool result)
    {
        result = false;
        binary = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as BinaryExpressionSyntax;
        return binary is not null && Sst1479MeaninglessCountComparisonAnalyzer.TryGetConstantResult(binary, out result);
    }

    /// <summary>Words the action with the constant the reported comparison folds to.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the comparison no longer folds.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetComparison(root, diagnostic, out _, out var result) switch
        {
            false => null,
            true when result => "Replace the comparison with 'true'",
            true => "Replace the comparison with 'false'",
        };

    /// <summary>Resolves a diagnostic to the comparison it reported, when that comparison still folds.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The comparison, or <see langword="null"/> when the reported shape no longer folds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BinaryExpressionSyntax? FindFoldingComparison(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetComparison(root, diagnostic, out var binary, out _) ? binary : null;

    /// <summary>Replaces a comparison with its constant, folded from the form it has when the batch applies the edit.</summary>
    /// <param name="node">The comparison, including any nested batch edits.</param>
    /// <returns>The boolean literal, or <paramref name="node"/> when it no longer folds.</returns>
    /// <remarks>
    /// The constant depends only on the operator and the literal operand, and no nested edit can reach either,
    /// so this fold always agrees with the one made on the original comparison.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxNode FoldCurrent(SyntaxNode node) =>
        node is BinaryExpressionSyntax binary && Sst1479MeaninglessCountComparisonAnalyzer.TryGetConstantResult(binary, out var result)
            ? BuildLiteral(binary, result)
            : node;

    /// <summary>Builds the boolean literal that takes the comparison's place, keeping its trivia.</summary>
    /// <param name="node">The comparison being replaced, including any nested batch edits.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <returns>The <c>true</c> or <c>false</c> literal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LiteralExpressionSyntax BuildLiteral(SyntaxNode node, bool result) =>
        SyntaxFactory.LiteralExpression(
            result ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression,
            SyntaxFactory.Token(node.GetLeadingTrivia(), result ? SyntaxKind.TrueKeyword : SyntaxKind.FalseKeyword, node.GetTrailingTrivia()));
}
