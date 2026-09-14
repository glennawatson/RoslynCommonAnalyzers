// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a "yoda" comparison into its natural reading by swapping the operands and flipping the
/// relational operator (SST1131): <c>0 &lt; count</c> becomes <c>count &gt; 0</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1131UseReadableConditionsCodeFixProvider))]
[Shared]
public sealed class Sst1131UseReadableConditionsCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(ReportedNode.Find<BinaryExpressionSyntax>, static (current, _) => BuildSwapped((BinaryExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseReadableConditions.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Swap operands",
            nameof(Sst1131UseReadableConditionsCodeFixProvider),
            ReportedNode.Find<BinaryExpressionSyntax>,
            BuildSwapped);

    /// <summary>Builds the comparison with its operands swapped and the operator flipped.</summary>
    /// <param name="comparison">The comparison to rewrite.</param>
    /// <returns>The rewritten comparison.</returns>
    internal static BinaryExpressionSyntax BuildSwapped(BinaryExpressionSyntax comparison)
    {
        var newLeft = comparison.Right
            .WithLeadingTrivia(comparison.Left.GetLeadingTrivia())
            .WithTrailingTrivia(comparison.Left.GetTrailingTrivia());
        var newRight = comparison.Left
            .WithLeadingTrivia(comparison.Right.GetLeadingTrivia())
            .WithTrailingTrivia(comparison.Right.GetTrailingTrivia());

        var flipped = ComparisonKinds.Mirror(comparison.Kind());
        var operatorToken = SyntaxFactory.Token(
            comparison.OperatorToken.LeadingTrivia,
            OperatorTokenKind(flipped),
            comparison.OperatorToken.TrailingTrivia);

        return SyntaxFactory.BinaryExpression(flipped, newLeft, operatorToken, newRight);
    }

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
        _ => SyntaxKind.GreaterThanEqualsToken
    };
}
