// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a "yoda" comparison into its natural reading by swapping the operands and flipping the
/// relational operator (SST1131): <c>0 &lt; count</c> becomes <c>count &gt; 0</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseReadableConditionsCodeFixProvider))]
[Shared]
public sealed class UseReadableConditionsCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseReadableConditions.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax comparison)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Swap operands",
                    _ => Task.FromResult(Swap(context.Document, root, comparison)),
                    equivalenceKey: nameof(UseReadableConditionsCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Swaps the operands of the comparison and flips the operator.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The comparison to rewrite.</param>
    /// <returns>The updated document.</returns>
    private static Document Swap(Document document, SyntaxNode root, BinaryExpressionSyntax comparison)
    {
        var newLeft = comparison.Right
            .WithLeadingTrivia(comparison.Left.GetLeadingTrivia())
            .WithTrailingTrivia(comparison.Left.GetTrailingTrivia());
        var newRight = comparison.Left
            .WithLeadingTrivia(comparison.Right.GetLeadingTrivia())
            .WithTrailingTrivia(comparison.Right.GetTrailingTrivia());

        var flipped = Flip(comparison.Kind());
        var operatorToken = SyntaxFactory.Token(
            comparison.OperatorToken.LeadingTrivia,
            OperatorTokenKind(flipped),
            comparison.OperatorToken.TrailingTrivia);

        var swapped = SyntaxFactory.BinaryExpression(flipped, newLeft, operatorToken, newRight);
        return document.WithSyntaxRoot(root.ReplaceNode(comparison, swapped));
    }

    /// <summary>Returns the comparison kind that reads the same after the operands are swapped.</summary>
    /// <param name="kind">The original comparison kind.</param>
    /// <returns>The flipped comparison kind.</returns>
    private static SyntaxKind Flip(SyntaxKind kind) => kind switch
    {
        SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
        _ => kind,
    };

    /// <summary>Returns the operator token kind for a comparison expression kind.</summary>
    /// <param name="kind">The comparison expression kind.</param>
    /// <returns>The matching operator token kind.</returns>
    private static SyntaxKind OperatorTokenKind(SyntaxKind kind) => kind switch
    {
        SyntaxKind.EqualsExpression => SyntaxKind.EqualsEqualsToken,
        SyntaxKind.NotEqualsExpression => SyntaxKind.ExclamationEqualsToken,
        SyntaxKind.LessThanExpression => SyntaxKind.LessThanToken,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.LessThanEqualsToken,
        SyntaxKind.GreaterThanExpression => SyntaxKind.GreaterThanToken,
        _ => SyntaxKind.GreaterThanEqualsToken,
    };
}
