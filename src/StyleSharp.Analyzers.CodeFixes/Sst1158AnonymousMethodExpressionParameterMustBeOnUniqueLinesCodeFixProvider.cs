// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1158AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1158AnonymousMethodExpressionParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1158AnonymousMethodExpressionParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1158AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

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

            if (node is AnonymousMethodExpressionSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.SST1150CodeFixTitle,
                        _ => FixAsync(context.Document, root, syntaxNode),
                        nameof(Sst1158AnonymousMethodExpressionParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    /// <summary>Rewrites the anonymous method expression so each parameter is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The anonymous method expression to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    private static Task<Document> FixAsync(Document document, SyntaxNode root, AnonymousMethodExpressionSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        var newNode = node.ConvertNodeIfAble(
                          node => node.ParameterList?.Parameters,
                          (node, parameters) => node.WithParameterList(
                              SyntaxFactory.ParameterList(parameters)
                                  .WithOpenParenToken(node.ParameterList!.OpenParenToken.WithTrailingTrivia(endOfLine))))
                      ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}
