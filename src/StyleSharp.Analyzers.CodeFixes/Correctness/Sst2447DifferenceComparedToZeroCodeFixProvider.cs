// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a difference compared against zero as a direct comparison of the operands (SST2447):
/// <c>a - b &gt; 0</c> becomes <c>a &gt; b</c>, and the mirrored <c>0 &lt; a - b</c> becomes the same thing.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2447DifferenceComparedToZeroCodeFixProvider))]
[Shared]
public sealed class Sst2447DifferenceComparedToZeroCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(CorrectnessRules.DifferenceComparedToZero.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Compare the operands directly",
            nameof(Sst2447DifferenceComparedToZeroCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported comparison and replaces it with the direct comparison.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BinaryExpressionSyntax>() is not { } comparison)
        {
            return null;
        }

        var subtractionOnLeft = Sst2447DifferenceComparedToZeroAnalyzer.Unwrap(comparison.Left)
            is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.SubtractExpression };
        var subtractionSide = subtractionOnLeft ? comparison.Left : comparison.Right;
        if (Sst2447DifferenceComparedToZeroAnalyzer.Unwrap(subtractionSide) is not BinaryExpressionSyntax
            { RawKind: (int)SyntaxKind.SubtractExpression } subtraction)
        {
            return null;
        }

        var text = Sst2447DifferenceComparedToZeroAnalyzer.RewrittenOperatorText((SyntaxKind)comparison.RawKind, subtractionOnLeft);
        var operatorToken = SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), ComparisonToken(text), SyntaxFactory.TriviaList(SyntaxFactory.Space));
        var replacement = SyntaxFactory.BinaryExpression(
                ComparisonKind(text),
                subtraction.Left.WithoutTrivia(),
                operatorToken,
                subtraction.Right.WithoutTrivia())
            .WithTriviaFrom(comparison);

        return new NodeReplacement(comparison, replacement);
    }

    /// <summary>Maps the rewritten operator text to its expression kind.</summary>
    /// <param name="text">The operator text.</param>
    /// <returns>The binary expression kind.</returns>
    private static SyntaxKind ComparisonKind(string text) => text switch
    {
        ">" => SyntaxKind.GreaterThanExpression,
        ">=" => SyntaxKind.GreaterThanOrEqualExpression,
        "<" => SyntaxKind.LessThanExpression,
        "<=" => SyntaxKind.LessThanOrEqualExpression,
        "==" => SyntaxKind.EqualsExpression,
        _ => SyntaxKind.NotEqualsExpression,
    };

    /// <summary>Maps the rewritten operator text to its token kind.</summary>
    /// <param name="text">The operator text.</param>
    /// <returns>The operator token kind.</returns>
    private static SyntaxKind ComparisonToken(string text) => text switch
    {
        ">" => SyntaxKind.GreaterThanToken,
        ">=" => SyntaxKind.GreaterThanEqualsToken,
        "<" => SyntaxKind.LessThanToken,
        "<=" => SyntaxKind.LessThanEqualsToken,
        "==" => SyntaxKind.EqualsEqualsToken,
        _ => SyntaxKind.ExclamationEqualsToken,
    };
}
