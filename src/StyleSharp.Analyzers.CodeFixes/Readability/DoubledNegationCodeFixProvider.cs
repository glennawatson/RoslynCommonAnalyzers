// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Collapses a run of doubled prefix-negation operators (SST1190).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DoubledNegationCodeFixProvider))]
[Shared]
public sealed class DoubledNegationCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoDoubledNegation.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is not PrefixUnaryExpressionSyntax unary)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the doubled operator",
                    _ => Task.FromResult(Apply(context.Document, root, unary)),
                    equivalenceKey: nameof(DoubledNegationCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Peels every consecutive same-kind operator, keeping one only for an odd count.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="unary">The outermost negation operator.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, PrefixUnaryExpressionSyntax unary)
    {
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

        return document.WithSyntaxRoot(root.ReplaceNode(unary, replacement.WithTriviaFrom(unary)));
    }
}
