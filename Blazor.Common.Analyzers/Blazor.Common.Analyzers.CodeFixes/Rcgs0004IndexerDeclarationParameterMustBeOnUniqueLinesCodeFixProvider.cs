// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Blazor.Common.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Rcgs0004IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Rcgs0004IndexerDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Rcgs0004IndexerDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [Rcgs0004IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId];

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

            if (node is IndexerDeclarationSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        _ => FixAsync(context.Document, root, syntaxNode),
                        nameof(Rcgs0004IndexerDeclarationParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    /// <summary>Rewrites the indexer declaration so each parameter is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The indexer declaration to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    private static Task<Document> FixAsync(Document document, SyntaxNode root, IndexerDeclarationSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        var newNode = node.ConvertNodeIfAble(
                          node => node.ParameterList?.Parameters,
                          (node, parameters) => node.WithParameterList(
                              SyntaxFactory.BracketedParameterList(parameters)
                                  .WithOpenBracketToken(node.ParameterList!.OpenBracketToken.WithTrailingTrivia(endOfLine))))
                      ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}
