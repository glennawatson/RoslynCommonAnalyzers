// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces SST2100/SST2101 collection creations with collection expressions.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionExpressionCodeFixProvider))]
[Shared]
public sealed class CollectionExpressionCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        CollectionExpressionRules.UseEmptyCollectionExpression.Id,
        CollectionExpressionRules.UseExplicitCollectionExpression.Id);

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

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax expression)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use collection expression",
                    cancellationToken => ReplaceAsync(context.Document, root, expression, diagnostic.Id),
                    equivalenceKey: nameof(CollectionExpressionCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Builds and applies the collection expression.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="expression">The expression to replace.</param>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The updated document.</returns>
    private static Task<Document> ReplaceAsync(Document document, SyntaxNode root, ExpressionSyntax expression, string diagnosticId)
    {
        var replacementText = "[]";
        if (diagnosticId == CollectionExpressionRules.UseExplicitCollectionExpression.Id
            && ExplicitCollectionExpressionAnalyzer.TryGetInitializer(expression, out var initializer))
        {
            var full = initializer!.ToFullString().AsSpan();
            replacementText = "[" + full[1..^1].ToString() + "]";
        }

        var replacement = SyntaxFactory.ParseExpression(replacementText).WithTriviaFrom(expression);
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(expression, replacement)));
    }
}
