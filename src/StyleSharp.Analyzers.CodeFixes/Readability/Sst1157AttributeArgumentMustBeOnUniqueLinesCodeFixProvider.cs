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
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, CodeFixResources.SST1150CodeFixTitle, nameof(Sst1157AttributeArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add", TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the attribute so each argument is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The attribute to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, AttributeSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported attribute and builds its arguments-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is AttributeSyntax node
            ? new NodeReplacement(node, Rewrite(node), static current => Rewrite((AttributeSyntax)current))
            : null;

    /// <summary>Builds the attribute with each argument moved to its own line.</summary>
    /// <param name="node">The attribute to rewrite.</param>
    /// <returns>The rewritten attribute, or the original when its argument list needs no change.</returns>
    private static AttributeSyntax Rewrite(AttributeSyntax node)
    {
        var endOfLine = UniqueLineCodeFixerHelper.GetEndOfLine(node, elastic: true);
        return node.ConvertNodeIfAble(
                   static inner => inner.ArgumentList?.Arguments,
                   (inner, arguments) => inner.WithArgumentList(
                       SyntaxFactory.AttributeArgumentList(arguments)
                           .WithOpenParenToken(inner.ArgumentList!.OpenParenToken.WithTrailingTrivia(endOfLine))))
               ?? node;
    }
}
