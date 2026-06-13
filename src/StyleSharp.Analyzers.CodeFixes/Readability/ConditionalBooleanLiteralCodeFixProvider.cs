// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a <c>c ? true : false</c> conditional with the condition itself, or its negation (SST1182).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConditionalBooleanLiteralCodeFixProvider))]
[Shared]
public sealed class ConditionalBooleanLiteralCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoConditionalBooleanLiteral.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not ConditionalExpressionSyntax conditional)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the condition directly",
                    _ => Task.FromResult(Apply(context.Document, root, conditional)),
                    equivalenceKey: nameof(ConditionalBooleanLiteralCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the conditional with the condition, negating it when the branches are swapped.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="conditional">The conditional expression returning boolean literals.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ConditionalExpressionSyntax conditional)
    {
        var condition = conditional.Condition.WithoutTrivia();
        var replacement = conditional.WhenTrue.IsKind(SyntaxKind.TrueLiteralExpression)
            ? condition
            : Negate(condition);

        return document.WithSyntaxRoot(root.ReplaceNode(conditional, replacement.WithTriviaFrom(conditional)));
    }

    /// <summary>Negates a boolean condition, unwrapping a double negation and parenthesizing when needed.</summary>
    /// <param name="condition">The condition to negate.</param>
    /// <returns>The negated condition.</returns>
    private static ExpressionSyntax Negate(ExpressionSyntax condition)
    {
        if (condition is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not)
        {
            return not.Operand;
        }

        var operand = condition is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ParenthesizedExpressionSyntax
            ? condition
            : SyntaxFactory.ParenthesizedExpression(condition);

        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand);
    }
}
