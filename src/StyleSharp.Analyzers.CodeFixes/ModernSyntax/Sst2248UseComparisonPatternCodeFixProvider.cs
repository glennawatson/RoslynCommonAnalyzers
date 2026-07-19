// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites two same-subject comparisons joined by <c>&amp;&amp;</c> or <c>||</c> as one
/// <c>is</c>-pattern (SST2248): <c>x &gt;= 0 &amp;&amp; x &lt;= 9</c> becomes
/// <c>x is &gt;= 0 and &lt;= 9</c>. The subject and both constants are reused from the original
/// nodes; only the operators and combinators are rebuilt, and the outer trivia is carried across so
/// nothing around the expression moves.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2248UseComparisonPatternCodeFixProvider))]
[Shared]
public sealed class Sst2248UseComparisonPatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseComparisonPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Combine these comparisons into an is-pattern", nameof(Sst2248UseComparisonPatternCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported combination and builds its is-pattern replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax binary
            || !Sst2248UseComparisonPatternAnalyzer.TryGetComparisonMerge(binary, model, out var merge))
        {
            return null;
        }

        var replacement = BuildReplacement(binary, merge);
        return new NodeReplacement(binary, replacement, _ => replacement);
    }

    /// <summary>Assembles the <c>subject is left CONN right</c> replacement for a reported combination.</summary>
    /// <param name="original">The reported logical expression, source of the outer trivia.</param>
    /// <param name="merge">The extracted merge pieces.</param>
    /// <returns>The is-pattern expression that replaces the combination.</returns>
    private static IsPatternExpressionSyntax BuildReplacement(BinaryExpressionSyntax original, ComparisonPatternMerge merge)
    {
        var pattern = SyntaxFactory.BinaryPattern(
            merge.IsConjunction ? SyntaxKind.AndPattern : SyntaxKind.OrPattern,
            BuildPattern(merge.LeftOperator, merge.LeftConstant),
            Keyword(merge.IsConjunction ? SyntaxKind.AndKeyword : SyntaxKind.OrKeyword),
            BuildPattern(merge.RightOperator, merge.RightConstant));

        return SyntaxFactory.IsPatternExpression(
                merge.Subject.WithoutTrivia(),
                Keyword(SyntaxKind.IsKeyword),
                pattern)
            .WithTriviaFrom(original);
    }

    /// <summary>Builds one comparison's pattern: a constant pattern for equality, else a relational pattern.</summary>
    /// <param name="comparison">The subject-on-left comparison kind.</param>
    /// <param name="constant">The constant the comparison tests against.</param>
    /// <returns>The pattern matching the comparison.</returns>
    private static PatternSyntax BuildPattern(SyntaxKind comparison, ExpressionSyntax constant)
    {
        var value = constant.WithoutTrivia();
        return comparison switch
        {
            SyntaxKind.EqualsExpression => SyntaxFactory.ConstantPattern(value),
            SyntaxKind.NotEqualsExpression => SyntaxFactory.UnaryPattern(
                SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.ConstantPattern(value)),
            _ => SyntaxFactory.RelationalPattern(
                SyntaxFactory.Token(RelationalToken(comparison)).WithTrailingTrivia(SyntaxFactory.Space),
                value),
        };
    }

    /// <summary>Maps a relational comparison kind to its pattern operator token.</summary>
    /// <param name="comparison">The subject-on-left relational comparison kind.</param>
    /// <returns>The token kind used in the relational pattern.</returns>
    private static SyntaxKind RelationalToken(SyntaxKind comparison)
        => comparison switch
        {
            SyntaxKind.LessThanExpression => SyntaxKind.LessThanToken,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.LessThanEqualsToken,
            SyntaxKind.GreaterThanExpression => SyntaxKind.GreaterThanToken,
            _ => SyntaxKind.GreaterThanEqualsToken,
        };

    /// <summary>Builds a contextual keyword token padded with a single space on each side.</summary>
    /// <param name="kind">The keyword kind.</param>
    /// <returns>The spaced keyword token.</returns>
    private static SyntaxToken Keyword(SyntaxKind kind)
        => SyntaxFactory.Token(kind).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space);
}
