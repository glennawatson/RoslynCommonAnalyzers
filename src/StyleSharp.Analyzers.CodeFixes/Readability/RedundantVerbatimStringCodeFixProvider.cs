// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Drops the <c>@</c> prefix from a verbatim string that needs no verbatim quoting (SST1184).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantVerbatimStringCodeFixProvider))]
[Shared]
public sealed class RedundantVerbatimStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantVerbatimString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not LiteralExpressionSyntax literal)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the '@' prefix",
                    _ => Task.FromResult(Apply(context.Document, root, literal)),
                    equivalenceKey: nameof(RedundantVerbatimStringCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not LiteralExpressionSyntax literal)
        {
            return;
        }

        var token = literal.Token;
        var regular = SyntaxFactory.Literal(token.LeadingTrivia, SyntaxFactory.Literal(token.ValueText).Text, token.ValueText, token.TrailingTrivia);
        editor.ReplaceNode(literal, literal.WithToken(regular));
    }

    /// <summary>Replaces the verbatim literal with a regular literal holding the same text.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="literal">The verbatim string literal.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, LiteralExpressionSyntax literal)
    {
        var token = literal.Token;
        var regular = SyntaxFactory.Literal(token.LeadingTrivia, SyntaxFactory.Literal(token.ValueText).Text, token.ValueText, token.TrailingTrivia);
        return document.WithSyntaxRoot(root.ReplaceToken(token, regular));
    }
}
