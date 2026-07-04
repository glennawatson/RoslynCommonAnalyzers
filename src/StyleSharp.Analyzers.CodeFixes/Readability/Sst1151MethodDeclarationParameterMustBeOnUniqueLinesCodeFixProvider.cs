// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1151MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1151MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1151MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1151MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, CodeFixResources.SST1150CodeFixTitle, nameof(Sst1151MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add", TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the method declaration so each parameter is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The method declaration to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, BaseMethodDeclarationSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported method declaration and builds its parameters-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is BaseMethodDeclarationSyntax node
            ? new NodeReplacement(node, Rewrite(node))
            : null;

    /// <summary>Builds the declaration with each parameter moved to its own line.</summary>
    /// <param name="node">The method declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    private static BaseMethodDeclarationSyntax Rewrite(BaseMethodDeclarationSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        return node.ConvertNodeIfAble(
                   node => node.ParameterList?.Parameters,
                   (node, parameters) => node.WithParameterList(
                       SyntaxFactory.ParameterList(parameters)
                           .WithOpenParenToken(node.ParameterList!.OpenParenToken.WithTrailingTrivia(endOfLine))))
               ?? node;
    }
}
