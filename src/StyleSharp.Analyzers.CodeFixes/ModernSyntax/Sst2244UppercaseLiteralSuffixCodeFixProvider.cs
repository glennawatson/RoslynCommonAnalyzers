// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Upper-cases a numeric literal's type suffix (SST2244): <c>1l</c> becomes <c>1L</c> and
/// <c>1ul</c> becomes <c>1UL</c>. Only the suffix characters change — everything before them,
/// including a hex literal's digits and any digit separators, is copied through verbatim, so
/// <c>0xffl</c> becomes <c>0xffL</c> and not <c>0xFFL</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2244UppercaseLiteralSuffixCodeFixProvider))]
[Shared]
public sealed class Sst2244UppercaseLiteralSuffixCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UppercaseLiteralSuffix.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Upper-case the literal suffix", nameof(Sst2244UppercaseLiteralSuffixCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces one reported literal with its upper-cased form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="literal">The reported literal.</param>
    /// <returns>The updated document, or the original document when the shape no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, LiteralExpressionSyntax literal)
        => Rewrite(literal) is { } replacement
            ? document.WithSyntaxRoot(root.ReplaceNode(literal, replacement))
            : document;

    /// <summary>Resolves the reported literal and builds its upper-cased replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not LiteralExpressionSyntax literal)
        {
            return null;
        }

        return Rewrite(literal) is { } replacement ? new NodeReplacement(literal, replacement) : null;
    }

    /// <summary>Builds the literal with its suffix upper-cased and its digits untouched.</summary>
    /// <param name="literal">The reported literal.</param>
    /// <returns>The rewritten literal carrying the original trivia, or <see langword="null"/> when the shape no longer matches.</returns>
    private static ExpressionSyntax? Rewrite(LiteralExpressionSyntax literal)
    {
        var text = literal.Token.Text;
        if (!literal.IsKind(SyntaxKind.NumericLiteralExpression)
            || !Sst2244UppercaseLiteralSuffixAnalyzer.TryGetLowercaseSuffix(text, out var suffixStart))
        {
            return null;
        }

        var upperCased = text.Substring(0, suffixStart) + text.Substring(suffixStart).ToUpperInvariant();
        return SyntaxFactory.ParseExpression(upperCased).WithTriviaFrom(literal);
    }
}
