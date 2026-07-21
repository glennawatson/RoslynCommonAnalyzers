// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a single-line raw string literal as a regular literal (SST2262): <c>"""plain text"""</c> becomes
/// <c>"plain text"</c>. The content has no character a regular literal must escape, so the value stays
/// character-for-character identical.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2262UseRegularStringLiteralCodeFixProvider))]
[Shared]
public sealed class Sst2262UseRegularStringLiteralCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseRegularStringLiteral.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use a regular string literal", nameof(Sst2262UseRegularStringLiteralCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported raw string literal and rewrites it to a regular literal.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not LiteralExpressionSyntax literal
            || !literal.Token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
            || !Sst2262UseRegularStringLiteralAnalyzer.IsPlainContent(literal.Token.ValueText))
        {
            return null;
        }

        var replacement = SyntaxFactory
            .LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(literal.Token.ValueText))
            .WithTriviaFrom(literal);

        return new NodeReplacement(literal, replacement);
    }
}
