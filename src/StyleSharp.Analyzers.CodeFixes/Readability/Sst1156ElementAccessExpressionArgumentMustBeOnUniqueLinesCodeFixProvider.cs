// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, CodeFixResources.SST1150CodeFixTitle, nameof(Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add", TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the element access expression so each argument is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The element access expression to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, ElementAccessExpressionSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported element access expression and builds its arguments-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is ElementAccessExpressionSyntax node
            ? new NodeReplacement(node, Rewrite(node), static current => Rewrite((ElementAccessExpressionSyntax)current))
            : null;

    /// <summary>Builds the element access expression with each argument moved to its own line.</summary>
    /// <param name="node">The element access expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when its bracketed argument list needs no change.</returns>
    private static ElementAccessExpressionSyntax Rewrite(ElementAccessExpressionSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        return node.ConvertNodeIfAble(
                   static inner => inner.ArgumentList?.Arguments,
                   (inner, arguments) => inner.WithArgumentList(
                       SyntaxFactory.BracketedArgumentList(arguments)
                           .WithOpenBracketToken(inner.ArgumentList!.OpenBracketToken.WithTrailingTrivia(endOfLine))))
               ?? node;
    }
}
