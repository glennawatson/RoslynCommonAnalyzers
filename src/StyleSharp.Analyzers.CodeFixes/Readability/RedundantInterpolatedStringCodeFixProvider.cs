// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an interpolation-free interpolated string as a plain string literal (SST1183).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantInterpolatedStringCodeFixProvider))]
[Shared]
public sealed class RedundantInterpolatedStringCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantInterpolatedString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is not InterpolatedStringExpressionSyntax interpolated)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the '$' prefix",
                    _ => Task.FromResult(Apply(context.Document, root, interpolated)),
                    equivalenceKey: nameof(RedundantInterpolatedStringCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Builds a regular string literal from the interpolated string's constant text.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="interpolated">The interpolation-free interpolated string.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InterpolatedStringExpressionSyntax interpolated)
    {
        // The compiler merges adjacent text into one token, so an interpolation-free string holds 0 or 1 content.
        var value = interpolated.Contents.Count == 1 && interpolated.Contents[0] is InterpolatedStringTextSyntax text
            ? text.TextToken.ValueText
            : string.Empty;

        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value));
        return document.WithSyntaxRoot(root.ReplaceNode(interpolated, literal.WithTriviaFrom(interpolated)));
    }
}
