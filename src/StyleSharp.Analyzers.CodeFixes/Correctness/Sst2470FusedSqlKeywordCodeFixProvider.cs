// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Inserts the missing space at a fused SQL keyword seam (SST2470) by giving the right literal a leading space,
/// so the two literals no longer run together — the concatenated text is identical wherever the single space
/// lands, so the edit is unambiguous. The fix is offered only when the right operand is a regular string literal;
/// a verbatim or raw right literal is reported without a fix, since rewriting it would rather change its form
/// than just its content, and the author is left to place the space.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2470FusedSqlKeywordCodeFixProvider))]
[Shared]
public sealed class Sst2470FusedSqlKeywordCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.FusedSqlKeyword.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Add a space between the concatenated string literals", nameof(Sst2470FusedSqlKeywordCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported concatenation and gives its right literal a leading space.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The right literal and its spaced replacement, or <see langword="null"/> when no safe fix applies.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not { } node
            || (node as BinaryExpressionSyntax ?? node.FirstAncestorOrSelf<BinaryExpressionSyntax>()) is not { } binary
            || !binary.IsKind(SyntaxKind.AddExpression)
            || Sst2470FusedSqlKeywordAnalyzer.TryGetFusedSeam(binary.Left, binary.Right) is null)
        {
            return null;
        }

        if (binary.Right is not LiteralExpressionSyntax rightLiteral
            || !rightLiteral.Token.IsKind(SyntaxKind.StringLiteralToken)
            || IsVerbatim(rightLiteral.Token))
        {
            return null;
        }

        return new NodeReplacement(rightLiteral, WithLeadingSpace(rightLiteral), current => WithLeadingSpace((LiteralExpressionSyntax)current));
    }

    /// <summary>Returns whether a string-literal token is verbatim (<c>@"..."</c>).</summary>
    /// <param name="token">The string-literal token.</param>
    /// <returns><see langword="true"/> when the token text is verbatim.</returns>
    private static bool IsVerbatim(SyntaxToken token)
        => token.Text.Length > 0 && token.Text[0] == '@';

    /// <summary>Rebuilds a regular string literal with one leading space added to its value.</summary>
    /// <param name="literal">The literal to space.</param>
    /// <returns>The literal whose value begins with a space.</returns>
    private static LiteralExpressionSyntax WithLeadingSpace(LiteralExpressionSyntax literal)
    {
        var token = literal.Token;
        var spaced = SyntaxFactory.Literal(" " + token.ValueText).WithTriviaFrom(token);
        return literal.WithToken(spaced);
    }
}
