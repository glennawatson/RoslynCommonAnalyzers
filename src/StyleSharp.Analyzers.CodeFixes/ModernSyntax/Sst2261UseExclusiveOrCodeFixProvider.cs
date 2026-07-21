// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an exclusive-or reimplementation as <c>x ^ y</c> (SST2261). The two operands are the side-effect-free
/// values from the original conjunctions, and the whole expression's trivia carries through.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2261UseExclusiveOrCodeFixProvider))]
[Shared]
public sealed class Sst2261UseExclusiveOrCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseExclusiveOr.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the exclusive-or operator", nameof(Sst2261UseExclusiveOrCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported disjunction and rewrites it to an exclusive-or.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is BinaryExpressionSyntax binary
                && (binary.IsKind(SyntaxKind.LogicalOrExpression) || binary.IsKind(SyntaxKind.BitwiseOrExpression)))
            {
                return Sst2261UseExclusiveOrAnalyzer.TryMatch(binary, out var x, out var y)
                    ? new NodeReplacement(binary, Build(binary, x, y))
                    : null;
            }
        }

        return null;
    }

    /// <summary>Builds the <c>x ^ y</c> expression replacing the reported expression.</summary>
    /// <param name="binary">The reported expression, used for its trivia.</param>
    /// <param name="x">The first exclusive-or operand.</param>
    /// <param name="y">The second exclusive-or operand.</param>
    /// <returns>The replacement expression.</returns>
    private static BinaryExpressionSyntax Build(BinaryExpressionSyntax binary, ExpressionSyntax x, ExpressionSyntax y)
    {
        var caret = SyntaxFactory.Token(SyntaxKind.CaretToken)
            .WithLeadingTrivia(SyntaxFactory.Space)
            .WithTrailingTrivia(SyntaxFactory.Space);
        return SyntaxFactory
            .BinaryExpression(SyntaxKind.ExclusiveOrExpression, x.WithoutTrivia(), caret, y.WithoutTrivia())
            .WithTriviaFrom(binary);
    }
}
