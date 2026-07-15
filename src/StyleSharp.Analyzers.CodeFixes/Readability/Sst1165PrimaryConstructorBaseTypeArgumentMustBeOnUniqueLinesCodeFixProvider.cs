// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, CodeFixResources.SST1150CodeFixTitle, nameof(Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add", TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the primary constructor base type so each argument is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The primary constructor base type to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, PrimaryConstructorBaseTypeSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported primary constructor base type and builds its arguments-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is PrimaryConstructorBaseTypeSyntax node
            ? new NodeReplacement(node, Rewrite(node), static current => Rewrite((PrimaryConstructorBaseTypeSyntax)current))
            : null;

    /// <summary>Builds the primary constructor base type with each argument moved to its own line.</summary>
    /// <param name="node">The primary constructor base type to rewrite.</param>
    /// <returns>The rewritten base type, or the original when it has no argument list.</returns>
    private static PrimaryConstructorBaseTypeSyntax Rewrite(PrimaryConstructorBaseTypeSyntax node)
        => UniqueLineCodeFixerHelper.SplitArgumentsOntoOwnLines(
            node,
            static inner => inner.ArgumentList,
            static (inner, list) => inner.WithArgumentList(list));
}
