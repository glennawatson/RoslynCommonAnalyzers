// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

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
            if (TryFindImplicitObjectCreation(root, diagnostic, out var syntaxNode))
            {
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

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryFindImplicitObjectCreation(editor.OriginalRoot, diagnostic, out var node))
        {
            return;
        }

        editor.ReplaceNode(node, (current, _) =>
        {
            var creation = (ImplicitObjectCreationExpressionSyntax)current;
            var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(creation, elastic: true);
            return creation.ConvertNodeIfAble(
                       node => node.ArgumentList?.Arguments,
                       (node, parameters) => node.WithArgumentList(
                           SyntaxFactory.ArgumentList(parameters)
                               .WithOpenParenToken(node.ArgumentList!.OpenParenToken.WithTrailingTrivia(endOfLine))))
                   ?? creation;
        });
    }

    /// <summary>Rewrites the argument expression so each argument is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The argument expression to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, ImplicitObjectCreationExpressionSyntax node)
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

    /// <summary>Finds the implicit object creation reported by the diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="node">The implicit object creation node.</param>
    /// <returns><see langword="true"/> when the diagnostic resolves to an implicit object creation.</returns>
    private static bool TryFindImplicitObjectCreation(
        SyntaxNode root,
        Diagnostic diagnostic,
        out ImplicitObjectCreationExpressionSyntax node)
    {
        var candidate = root
            .FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ImplicitObjectCreationExpressionSyntax>();
        if (candidate is null)
        {
            node = null!;
            return false;
        }

        node = candidate;
        return true;
    }
}
