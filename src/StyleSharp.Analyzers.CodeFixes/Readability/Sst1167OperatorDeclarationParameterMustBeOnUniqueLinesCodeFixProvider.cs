// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1167OperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1167OperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1167OperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1167OperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, CodeFixResources.SST1150CodeFixTitle, nameof(Sst1167OperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add", TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the operator declaration so each parameter is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The operator declaration to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, OperatorDeclarationSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported operator declaration and builds its parameters-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is OperatorDeclarationSyntax node
            ? new NodeReplacement(node, Rewrite(node), static current => Rewrite((OperatorDeclarationSyntax)current))
            : null;

    /// <summary>Builds the operator declaration with each parameter moved to its own line.</summary>
    /// <param name="node">The operator declaration to rewrite.</param>
    /// <returns>The rewritten declaration, or the original when it has no parameter list.</returns>
    private static OperatorDeclarationSyntax Rewrite(OperatorDeclarationSyntax node)
        => UniqueLineCodeFixerHelper.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));
}
