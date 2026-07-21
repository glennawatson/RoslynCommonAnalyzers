// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites <c>x == null</c> / <c>x != null</c> on an unconstrained type parameter (SST2493) to the
/// constant pattern <c>x is null</c> / <c>x is not null</c>, which is correct for every substitution and
/// never boxes. The non-null operand is kept; the whole comparison takes its place.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2493NullComparisonOnUnconstrainedGenericCodeFixProvider))]
[Shared]
public sealed class Sst2493NullComparisonOnUnconstrainedGenericCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(CorrectnessRules.NullComparisonOnUnconstrainedGeneric.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use 'is null' / 'is not null'",
            nameof(Sst2493NullComparisonOnUnconstrainedGenericCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported comparison and builds its constant-pattern replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BinaryExpressionSyntax>() is not { } binary
            || GetOperandComparedToNull(binary) is not { } operand)
        {
            return null;
        }

        var pattern = NullPattern(binary.IsKind(SyntaxKind.NotEqualsExpression));
        var isKeyword = SyntaxFactory.Token(SyntaxKind.IsKeyword)
            .WithLeadingTrivia(SyntaxFactory.Space)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var replacement = SyntaxFactory.IsPatternExpression(operand.WithoutTrivia(), isKeyword, pattern)
            .WithTriviaFrom(binary);
        return new NodeReplacement(binary, replacement);
    }

    /// <summary>Returns the non-null operand of an equality whose other operand is the null literal.</summary>
    /// <param name="binary">The equality expression.</param>
    /// <returns>The non-null operand, or <see langword="null"/> when neither operand is the null literal.</returns>
    private static ExpressionSyntax? GetOperandComparedToNull(BinaryExpressionSyntax binary)
    {
        if (binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return binary.Left;
        }

        return binary.Left.IsKind(SyntaxKind.NullLiteralExpression) ? binary.Right : null;
    }

    /// <summary>Builds the <c>null</c> constant pattern, negated for a not-equal comparison.</summary>
    /// <param name="negated">Whether the comparison was <c>!=</c>.</param>
    /// <returns>The pattern <c>null</c> or <c>not null</c>.</returns>
    private static PatternSyntax NullPattern(bool negated)
    {
        var constant = SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
        if (!negated)
        {
            return constant;
        }

        var notKeyword = SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        return SyntaxFactory.UnaryPattern(notKeyword, constant);
    }
}
