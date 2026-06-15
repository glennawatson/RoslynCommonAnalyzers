// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1157AttributeArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1157AttributeArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1157AttributeArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1157AttributeArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

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
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is AttributeSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.SST1150CodeFixTitle,
                        _ => FixAsync(context.Document, root, syntaxNode),
                        nameof(Sst1157AttributeArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not AttributeSyntax node)
        {
            return;
        }

        editor.ReplaceNode(node, BuildNode(node));
    }

    /// <summary>Rewrites the attribute so each parameter is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The attribute to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, AttributeSyntax node)
    {
        var newNode = BuildNode(node);
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }

    /// <summary>Builds the rewritten attribute with each argument on its own line.</summary>
    /// <param name="node">The attribute to rewrite.</param>
    /// <returns>The rewritten attribute.</returns>
    private static AttributeSyntax BuildNode(AttributeSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        return node.ConvertNodeIfAble(
                   node => node.ArgumentList?.Arguments,
                   (node, parameters) => node.WithArgumentList(
                       SyntaxFactory.AttributeArgumentList(parameters)
                           .WithOpenParenToken(node.ArgumentList!.OpenParenToken.WithTrailingTrivia(endOfLine))))
               ?? node;
    }
}
