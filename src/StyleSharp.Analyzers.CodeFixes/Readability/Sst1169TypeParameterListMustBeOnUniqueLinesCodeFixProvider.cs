// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1169TypeParameterListMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1169TypeParameterListMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1169TypeParameterListMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1169TypeParameterListMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, CodeFixResources.SST1150CodeFixTitle, nameof(Sst1169TypeParameterListMustBeOnUniqueLinesCodeFixProvider) + "-Add", TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the type parameter list so each type parameter is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The type parameter list to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, TypeParameterListSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported type parameter list and builds its entries-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is TypeParameterListSyntax node
            ? new NodeReplacement(node, Rewrite(node), static current => Rewrite((TypeParameterListSyntax)current))
            : null;

    /// <summary>Builds the type parameter list with each type parameter moved to its own line.</summary>
    /// <param name="node">The type parameter list to rewrite.</param>
    /// <returns>The rewritten list, or the original when it needs no change.</returns>
    private static TypeParameterListSyntax Rewrite(TypeParameterListSyntax node)
        => UniqueLineCodeFixerHelper.SplitAngleBracketedListOntoOwnLines(
            node,
            node.Parameters,
            (list, endOfLine) => SyntaxFactory.TypeParameterList(list)
                .WithLessThanToken(node.LessThanToken.WithTrailingTrivia(endOfLine))
                .WithGreaterThanToken(node.GreaterThanToken));
}
