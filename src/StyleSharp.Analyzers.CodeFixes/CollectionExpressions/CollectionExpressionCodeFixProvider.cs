// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Replaces SST2100/SST2101 collection creations with collection expressions.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionExpressionCodeFixProvider))]
[Shared]
public sealed class CollectionExpressionCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        CollectionExpressionRules.UseEmptyCollectionExpression.Id,
        CollectionExpressionRules.UseExplicitCollectionExpression.Id,
        CollectionExpressionRules.UseCollectionExpressionForStackalloc.Id,
        CollectionExpressionRules.UseCollectionExpressionForCreate.Id,
        CollectionExpressionRules.UseCollectionExpressionForFluent.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

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
            if (FindExpression(root, diagnostic) is not { } expression)
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

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (FindExpression(editor.OriginalRoot, diagnostic) is not { } expression)
        {
            return;
        }

        editor.ReplaceNode(expression, (current, _) => BuildReplacement((ExpressionSyntax)current, diagnostic.Id));
    }

    /// <summary>Builds and applies the collection expression.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="expression">The expression to replace.</param>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The updated document.</returns>
    internal static Task<Document> ReplaceAsync(Document document, SyntaxNode root, ExpressionSyntax expression, string diagnosticId)
    {
        var replacement = BuildReplacement(expression, diagnosticId);
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(expression, replacement)));
    }

    /// <summary>Resolves a diagnostic to the collection creation it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported expression, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionSyntax? FindExpression(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as ExpressionSyntax;

    /// <summary>Builds the collection-expression replacement for an offending creation.</summary>
    /// <param name="expression">The expression to replace.</param>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The collection-expression replacement, carrying the original trivia.</returns>
    private static CollectionExpressionSyntax BuildReplacement(ExpressionSyntax expression, string diagnosticId)
    {
        var replacementText = "[]";
        if (diagnosticId == CollectionExpressionRules.UseExplicitCollectionExpression.Id
            && Sst2101ExplicitCollectionExpressionAnalyzer.TryGetInitializer(expression, out var initializer))
        {
            replacementText = CollectionExpressionAdvancedAnalysis.CollectionExpressionText(initializer!);
        }
        else if (diagnosticId == CollectionExpressionRules.UseCollectionExpressionForStackalloc.Id
            && CollectionExpressionAdvancedAnalysis.TryGetStackallocInitializer(expression, out var stackallocInitializer))
        {
            replacementText = CollectionExpressionAdvancedAnalysis.CollectionExpressionText(stackallocInitializer);
        }
        else if ((diagnosticId == CollectionExpressionRules.UseCollectionExpressionForCreate.Id
                || diagnosticId == CollectionExpressionRules.UseCollectionExpressionForFluent.Id)
            && expression is InvocationExpressionSyntax invocation
            && CollectionExpressionAdvancedAnalysis.TryBuildInvocationCollectionExpression(invocation, out var invocationText))
        {
            replacementText = invocationText;
        }

        var replacement = (CollectionExpressionSyntax)SyntaxFactory.ParseExpression(replacementText);
        return replacement.Update(
            replacement.OpenBracketToken.WithLeadingTrivia(expression.GetLeadingTrivia()),
            replacement.Elements,
            replacement.CloseBracketToken.WithTrailingTrivia(expression.GetTrailingTrivia()));
    }
}
