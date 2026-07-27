// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Collapses a run of doubled prefix-negation operators (SST1190).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DoubledNegationCodeFixProvider))]
[Shared]
public sealed class DoubledNegationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoDoubledNegation.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the doubled operator", nameof(DoubledNegationCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported node and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not PrefixUnaryExpressionSyntax unary)
        {
            return null;
        }

        var count = 0;
        ExpressionSyntax current = unary;
        while (ExpressionSimplificationAnalyzer.Unwrap(current) is PrefixUnaryExpressionSyntax peeled && peeled.IsKind(unary.Kind()))
        {
            count++;
            current = peeled.Operand;
        }

        var operand = ExpressionSimplificationAnalyzer.Unwrap(current).WithoutTrivia();
        ExpressionSyntax replacement = count % 2 == 0
            ? operand
            : SyntaxFactory.PrefixUnaryExpression(unary.Kind(), operand);

        return new NodeReplacement(unary, replacement.WithTriviaFrom(unary));
    }
}
