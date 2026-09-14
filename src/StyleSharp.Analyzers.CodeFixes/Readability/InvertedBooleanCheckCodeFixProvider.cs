// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an inverted comparison (SST1172) to use the opposite operator, dropping the <c>!</c>.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InvertedBooleanCheckCodeFixProvider))]
[Shared]
public sealed class InvertedBooleanCheckCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoInvertedBooleanCheck.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use the opposite operator",
            nameof(InvertedBooleanCheckCodeFixProvider),
            static (root, diagnostic) => TryResolve(root, diagnostic, out _, out _, out _, out _),
            TryRewrite);

    /// <summary>Resolves the reported logical-not and builds the opposite comparison that replaces it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryResolve(root, diagnostic, out var not, out var binary, out var expressionKind, out var tokenKind)
            ? new NodeReplacement(not, CreateOpposite(not, binary, expressionKind, tokenKind))
            : null;

    /// <summary>Resolves the reported logical-not and the comparison it wraps, when that comparison has an opposite operator.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="not">The logical-not expression to rewrite.</param>
    /// <param name="binary">The comparison wrapped by the logical-not.</param>
    /// <param name="expressionKind">The opposite comparison's expression kind.</param>
    /// <param name="tokenKind">The opposite comparison's operator token kind.</param>
    /// <returns><see langword="true"/> when the reported shape still has an opposite form.</returns>
    private static bool TryResolve(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out PrefixUnaryExpressionSyntax? not,
        [NotNullWhen(true)] out BinaryExpressionSyntax? binary,
        out SyntaxKind expressionKind,
        out SyntaxKind tokenKind)
    {
        expressionKind = default;
        tokenKind = default;
        if (root.FindNode(diagnostic.Location.SourceSpan) is not PrefixUnaryExpressionSyntax logicalNot
            || ExpressionShapes.WalkDownParentheses(logicalNot.Operand) is not BinaryExpressionSyntax comparison)
        {
            not = null;
            binary = null;
            return false;
        }

        not = logicalNot;
        binary = comparison;
        return ExpressionSimplificationAnalyzer.TryGetOpposite(comparison.Kind(), out expressionKind, out tokenKind, out _);
    }

    /// <summary>Builds the opposite comparison that takes the logical-not's place and trivia.</summary>
    /// <param name="not">The logical-not expression being replaced.</param>
    /// <param name="binary">The comparison wrapped by the logical-not.</param>
    /// <param name="expressionKind">The opposite comparison's expression kind.</param>
    /// <param name="tokenKind">The opposite comparison's operator token kind.</param>
    /// <returns>The opposite comparison.</returns>
    private static BinaryExpressionSyntax CreateOpposite(
        PrefixUnaryExpressionSyntax not,
        BinaryExpressionSyntax binary,
        SyntaxKind expressionKind,
        SyntaxKind tokenKind)
    {
        var left = binary.Left.WithoutLeadingTrivia().WithTrailingTrivia(SyntaxFactory.Space);
        var operatorToken = SyntaxFactory.Token(default, tokenKind, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        var right = binary.Right.WithoutTrivia();
        return SyntaxFactory.BinaryExpression(expressionKind, left, operatorToken, right).WithTriviaFrom(not);
    }
}
