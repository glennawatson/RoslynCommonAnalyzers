// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Blazor.Common.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Rcgs0021TypeArgumentListMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Rcgs0021TypeArgumentListMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Rcgs0021TypeArgumentListMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [Rcgs0021TypeArgumentListMustBeOnUniqueLinesAnalyzer.DiagnosticId];

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
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is TypeArgumentListSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        _ => FixAsync(context.Document, root, syntaxNode),
                        nameof(Rcgs0021TypeArgumentListMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    /// <summary>Rewrites the list so each type argument is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The type argument list to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    private static Task<Document> FixAsync(Document document, SyntaxNode root, TypeArgumentListSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: false);
        var newList = UniqueLineCodeFixerHelper.SplitEntriesOntoOwnLines(node, node.Arguments);
        var newNode = newList is null
            ? node
            : SyntaxFactory.TypeArgumentList(newList.Value)
                .WithLessThanToken(node.LessThanToken.WithTrailingTrivia(endOfLine))
                .WithGreaterThanToken(node.GreaterThanToken);
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}
