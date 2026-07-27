// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an interpolation-free interpolated string as a plain string literal (SST1183).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantInterpolatedStringCodeFixProvider))]
[Shared]
public sealed class RedundantInterpolatedStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantInterpolatedString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the '$' prefix", nameof(RedundantInterpolatedStringCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported node and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InterpolatedStringExpressionSyntax interpolated)
        {
            return null;
        }

        // The compiler merges adjacent text into one token, so an interpolation-free string holds 0 or 1 content.
        var value = interpolated.Contents.Count == 1 && interpolated.Contents[0] is InterpolatedStringTextSyntax text
            ? text.TextToken.ValueText
            : string.Empty;

        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value));

        return new NodeReplacement(interpolated, literal.WithTriviaFrom(interpolated));
    }
}
