// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a negated pattern test as an <c>is not</c> pattern (SST2008). A pattern that is already
/// negated loses its <c>not</c> rather than gaining a second one, and the grouping the <c>!</c> needed
/// goes with it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2008IsNotPatternCodeFixProvider))]
[Shared]
public sealed class Sst2008IsNotPatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseIsNotPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use an 'is not' pattern", nameof(Sst2008IsNotPatternCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Negates a pattern, unwrapping one that is already negated.</summary>
    /// <param name="pattern">The pattern under the <c>!</c>.</param>
    /// <returns>The negated pattern.</returns>
    internal static PatternSyntax Negate(PatternSyntax pattern)
        => pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } negated
            ? negated.Pattern.WithoutLeadingTrivia()
            : SyntaxFactory.UnaryPattern(
                SyntaxFactory.Token(default, SyntaxKind.NotKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
                pattern.WithoutLeadingTrivia());

    /// <summary>Removes enclosing parentheses around an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The unwrapped expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Resolves the reported negation and builds its <c>is not</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpression
            && Unwrap(notExpression.Operand) is IsPatternExpressionSyntax isPattern
            ? new NodeReplacement(notExpression, isPattern.WithPattern(Negate(isPattern.Pattern)).WithTriviaFrom(notExpression))
            : null;
}
