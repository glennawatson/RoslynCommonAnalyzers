// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

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

            if (node is ImplicitObjectCreationExpressionSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.SST1150CodeFixTitle,
                        _ => FixAsync(context.Document, root, syntaxNode),
                        nameof(Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    /// <summary>Rewrites the argument expression so each argument is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The argument expression to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    private static Task<Document> FixAsync(Document document, SyntaxNode root, ImplicitObjectCreationExpressionSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        var newNode = node.ConvertNodeIfAble(
                          node => node.ArgumentList?.Arguments,
                          (node, parameters) => node.WithArgumentList(
                              SyntaxFactory.ArgumentList(parameters)
                                  .WithOpenParenToken(node.ArgumentList!.OpenParenToken.WithTrailingTrivia(endOfLine))))
                      ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}
