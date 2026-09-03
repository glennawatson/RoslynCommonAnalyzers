// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Folds a null guard into the member read it guards (SST2285): <c>x != null &amp;&amp; x.Count &gt; 0</c>
/// becomes <c>x?.Count &gt; 0</c>, a guarded bool member becomes <c>x?.Member == true</c>, and a
/// <c>bool?</c> read through <c>.Value</c> becomes <c>b == true</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2285FoldNullCheckIntoConditionalAccessCodeFixProvider))]
[Shared]
public sealed class Sst2285FoldNullCheckIntoConditionalAccessCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(ModernSyntaxRules.FoldNullCheckIntoConditionalAccess.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Fold into a conditional access",
            nameof(Sst2285FoldNullCheckIntoConditionalAccessCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported conjunction and replaces it with the folded form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BinaryExpressionSyntax>() is not { } conjunction
            || !conjunction.IsKind(SyntaxKind.LogicalAndExpression))
        {
            return null;
        }

        var kind = NullCheckConditionalAccessFold.Classify(conjunction, model, CancellationToken.None, out var receiver, out var use);
        var replacement = kind switch
        {
            NullCheckFoldKind.NullableBooleanValue => ComparedToTrue(receiver),
            NullCheckFoldKind.BooleanMember => BuildBooleanMember(receiver, use),
            NullCheckFoldKind.Comparison => BuildComparison(receiver, (BinaryExpressionSyntax)use),
            _ => null,
        };

        return replacement is null ? null : new NodeReplacement(conjunction, replacement.WithTriviaFrom(conjunction));
    }

    /// <summary>Builds <c>value == true</c>.</summary>
    /// <param name="value">The left operand.</param>
    /// <returns>The comparison expression.</returns>
    private static BinaryExpressionSyntax ComparedToTrue(ExpressionSyntax value)
        => SyntaxFactory.BinaryExpression(
            SyntaxKind.EqualsExpression,
            value.WithoutTrivia(),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.EqualsEqualsToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));

    /// <summary>Builds <c>receiver?.Member == true</c> for a guarded bool-valued member read.</summary>
    /// <param name="receiver">The guarded value.</param>
    /// <param name="use">The member read built on it.</param>
    /// <returns>The folded expression, or <see langword="null"/> when the chain no longer matches.</returns>
    private static BinaryExpressionSyntax? BuildBooleanMember(ExpressionSyntax receiver, ExpressionSyntax use)
        => ToConditionalAccess(receiver, use) is { } conditional ? ComparedToTrue(conditional) : null;

    /// <summary>Builds <c>receiver?.Member op constant</c> for a guarded comparison.</summary>
    /// <param name="receiver">The guarded value.</param>
    /// <param name="comparison">The comparison built on it.</param>
    /// <returns>The folded expression, or <see langword="null"/> when the chain no longer matches.</returns>
    /// <remarks>
    /// The rebuilt left operand takes the original left operand's trivia, because the space that separates it
    /// from the operator is that operand's trailing trivia and would otherwise be dropped.
    /// </remarks>
    private static BinaryExpressionSyntax? BuildComparison(ExpressionSyntax receiver, BinaryExpressionSyntax comparison)
        => ToConditionalAccess(receiver, comparison.Left) is { } conditional
            ? comparison.WithLeft(conditional.WithTriviaFrom(comparison.Left))
            : null;

    /// <summary>Rewrites a member chain rooted at a value into a conditional access on that value.</summary>
    /// <param name="receiver">The guarded value.</param>
    /// <param name="use">The member chain.</param>
    /// <returns>The conditional access, or <see langword="null"/> when the chain no longer matches.</returns>
    private static ConditionalAccessExpressionSyntax? ToConditionalAccess(ExpressionSyntax receiver, ExpressionSyntax use)
    {
        if (NullCheckConditionalAccessFold.FindRootAccess(use, receiver) is not { } rootAccess)
        {
            return null;
        }

        var binding = SyntaxFactory.MemberBindingExpression(rootAccess.Name.WithoutTrivia());
        var whenNotNull = use.ReplaceNode(rootAccess, binding).WithoutTrivia();
        return SyntaxFactory.ConditionalAccessExpression(receiver.WithoutTrivia(), whenNotNull);
    }
}
