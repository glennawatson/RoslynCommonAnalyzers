// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1170TypeArgumentListMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1170TypeArgumentListMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1170TypeArgumentListMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1170TypeArgumentListMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, CodeFixResources.SST1150CodeFixTitle, nameof(Sst1170TypeArgumentListMustBeOnUniqueLinesCodeFixProvider) + "-Add", TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the type argument list so each type argument is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The type argument list to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, TypeArgumentListSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported type argument list and builds its entries-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is TypeArgumentListSyntax node
            ? new NodeReplacement(node, Rewrite(node), static current => Rewrite((TypeArgumentListSyntax)current))
            : null;

    /// <summary>Builds the type argument list with each type argument moved to its own line.</summary>
    /// <param name="node">The type argument list to rewrite.</param>
    /// <returns>The rewritten list, or the original when it needs no change.</returns>
    private static TypeArgumentListSyntax Rewrite(TypeArgumentListSyntax node)
        => UniqueLineCodeFixerHelper.SplitAngleBracketedListOntoOwnLines(
            node,
            node.Arguments,
            (list, endOfLine) => SyntaxFactory.TypeArgumentList(list)
                .WithLessThanToken(node.LessThanToken.WithTrailingTrivia(endOfLine))
                .WithGreaterThanToken(node.GreaterThanToken));
}
