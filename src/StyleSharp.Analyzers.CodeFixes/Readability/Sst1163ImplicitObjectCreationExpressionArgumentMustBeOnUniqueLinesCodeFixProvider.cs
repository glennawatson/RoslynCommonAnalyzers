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
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            CodeFixResources.SST1150CodeFixTitle,
            nameof(Sst1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add",
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the implicit object creation expression so each argument is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The implicit object creation expression to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, ImplicitObjectCreationExpressionSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported implicit object creation expression and builds its arguments-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
               .FirstAncestorOrSelf<ImplicitObjectCreationExpressionSyntax>() is { } node
            ? new NodeReplacement(node, Rewrite(node), static current => Rewrite((ImplicitObjectCreationExpressionSyntax)current))
            : null;

    /// <summary>Builds the implicit object creation expression with each argument moved to its own line.</summary>
    /// <param name="node">The implicit object creation expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when it has no argument list.</returns>
    private static ImplicitObjectCreationExpressionSyntax Rewrite(ImplicitObjectCreationExpressionSyntax node)
        => UniqueLineCodeFixerHelper.SplitArgumentsOntoOwnLines(
            node,
            static inner => inner.ArgumentList,
            static (inner, list) => inner.WithArgumentList(list));
}
