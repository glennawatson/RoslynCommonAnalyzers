// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a <c>c ? true : false</c> conditional with the condition itself, or its negation (SST1182).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConditionalBooleanLiteralCodeFixProvider))]
[Shared]
public sealed class ConditionalBooleanLiteralCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoConditionalBooleanLiteral.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the condition directly", nameof(ConditionalBooleanLiteralCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported node and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not ConditionalExpressionSyntax conditional)
        {
            return null;
        }

        var condition = conditional.Condition.WithoutTrivia();
        var replacement = conditional.WhenTrue.IsKind(SyntaxKind.TrueLiteralExpression)
            ? condition
            : Negate(condition);

        return new NodeReplacement(conditional, replacement.WithTriviaFrom(conditional));
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
