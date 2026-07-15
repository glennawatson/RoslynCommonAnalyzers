// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1159ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1159ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1159ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1159ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            CodeFixResources.SST1150CodeFixTitle,
            nameof(Sst1159ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add",
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the parenthesized lambda expression so each parameter is placed on its own line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root of the document.</param>
    /// <param name="node">The parenthesized lambda expression to rewrite.</param>
    /// <returns>A task producing the updated document.</returns>
    internal static Task<Document> FixAsync(Document document, SyntaxNode root, ParenthesizedLambdaExpressionSyntax node)
        => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, Rewrite(node))));

    /// <summary>Resolves the reported parenthesized lambda expression and builds its parameters-on-unique-lines form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is ParenthesizedLambdaExpressionSyntax node
            ? new NodeReplacement(node, Rewrite(node), static current => Rewrite((ParenthesizedLambdaExpressionSyntax)current))
            : null;

    /// <summary>Builds the parenthesized lambda expression with each parameter moved to its own line.</summary>
    /// <param name="node">The parenthesized lambda expression to rewrite.</param>
    /// <returns>The rewritten expression, or the original when it has no parameter list.</returns>
    private static ParenthesizedLambdaExpressionSyntax Rewrite(ParenthesizedLambdaExpressionSyntax node)
        => UniqueLineCodeFixerHelper.SplitParametersOntoOwnLines(
            node,
            static inner => inner.ParameterList,
            static (inner, list) => inner.WithParameterList(list));
}
