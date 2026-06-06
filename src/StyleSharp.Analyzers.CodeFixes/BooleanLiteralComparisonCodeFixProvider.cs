// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant comparison to a boolean literal (SST1143), negating when needed.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BooleanLiteralComparisonCodeFixProvider))]
[Shared]
public sealed class BooleanLiteralComparisonCodeFixProvider : CodeFixProvider
{
    /// <summary>Expression kinds a prefix <c>!</c> binds to without needing parentheses.</summary>
    private static readonly HashSet<SyntaxKind> NegationSafeKinds =
    [
        SyntaxKind.IdentifierName,
        SyntaxKind.SimpleMemberAccessExpression,
        SyntaxKind.InvocationExpression,
        SyntaxKind.ElementAccessExpression,
        SyntaxKind.ParenthesizedExpression,
        SyntaxKind.ThisExpression,
        SyntaxKind.BaseExpression,
        SyntaxKind.TrueLiteralExpression,
        SyntaxKind.FalseLiteralExpression,
        SyntaxKind.LogicalNotExpression,
        SyntaxKind.ConditionalAccessExpression,
    ];

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoBooleanLiteralComparison.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax binary)
            {
                continue;
            }

            var replacement = Simplify(binary).WithTriviaFrom(binary);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the comparison",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(binary, replacement))),
                    equivalenceKey: nameof(BooleanLiteralComparisonCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Reduces a boolean-literal comparison to the operand, negated when the comparison was.</summary>
    /// <param name="binary">The equality expression.</param>
    /// <returns>The simplified expression.</returns>
    private static ExpressionSyntax Simplify(BinaryExpressionSyntax binary)
    {
        var leftLiteral = BooleanLiteralComparisonAnalyzer.IsBooleanLiteral(binary.Left);
        var literal = leftLiteral ? binary.Left : binary.Right;
        var operand = (leftLiteral ? binary.Right : binary.Left).WithoutTrivia();

        // 'x == true' / 'x != false' keep the operand; 'x == false' / 'x != true' negate it.
        var literalIsTrue = literal.IsKind(SyntaxKind.TrueLiteralExpression);
        var isEquals = binary.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken);
        return literalIsTrue == isEquals ? operand : Negate(operand);
    }

    /// <summary>Returns the logical negation of an operand, parenthesizing it when precedence requires.</summary>
    /// <param name="operand">The operand to negate.</param>
    /// <returns>The negated expression.</returns>
    private static PrefixUnaryExpressionSyntax Negate(ExpressionSyntax operand)
    {
        var inner = NegationSafeKinds.Contains(operand.Kind()) ? operand : SyntaxFactory.ParenthesizedExpression(operand);
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, inner);
    }
}
