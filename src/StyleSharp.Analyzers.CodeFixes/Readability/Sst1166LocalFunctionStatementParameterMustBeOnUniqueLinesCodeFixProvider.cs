// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1166LocalFunctionStatementParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1166LocalFunctionStatementParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1166LocalFunctionStatementParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1166LocalFunctionStatementParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

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

            if (node is LocalFunctionStatementSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.SST1150CodeFixTitle,
                        _ => FixAsync(context.Document, root, syntaxNode),
                        nameof(Sst1166LocalFunctionStatementParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not LocalFunctionStatementSyntax node)
        {
            return;
        }

        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        var newNode = node.ConvertNodeIfAble(
            param => param.ParameterList?.Parameters,
            (syntax, parameters) => syntax.WithParameterList(
                SyntaxFactory.ParameterList(parameters)
                    .WithOpenParenToken(syntax.ParameterList!.OpenParenToken.WithTrailingTrivia(endOfLine))));
        if (newNode is null)
        {
            return;
        }

        editor.ReplaceNode(node, newNode);
    }

    /// <summary>Rewrites the declaration so each parameter is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The declaration to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, LocalFunctionStatementSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        var newNode = node.ConvertNodeIfAble(
                          param => param.ParameterList?.Parameters,
                          (syntax, parameters) => syntax.WithParameterList(
                              SyntaxFactory.ParameterList(parameters)
                                  .WithOpenParenToken(syntax.ParameterList!.OpenParenToken.WithTrailingTrivia(endOfLine))))
                      ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}
