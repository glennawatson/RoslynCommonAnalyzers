// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites a negated type test <c>!(x is T)</c> as the <c>x is not T</c> pattern (SST2006).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NegatedIsPatternCodeFixProvider))]
[Shared]
public sealed class NegatedIsPatternCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(Resolve, static (current, _) => Rewrite((PrefixUnaryExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseNegatedIsPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Use the 'is not' pattern",
            nameof(NegatedIsPatternCodeFixProvider),
            Resolve,
            Rewrite);

    /// <summary>Resolves the reported logical-not expression when it still negates a plain type test.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The logical-not expression, or <see langword="null"/> when the shape no longer matches.</returns>
    private static PrefixUnaryExpressionSyntax? Resolve(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is PrefixUnaryExpressionSyntax negation
            && ExpressionShapes.WalkDownParentheses(negation.Operand) is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax }
            ? negation
            : null;

    /// <summary>Builds the <c>is not</c> pattern that replaces a negated type test.</summary>
    /// <param name="negation">The logical-not expression <see cref="Resolve"/> matched.</param>
    /// <returns>The <c>is not</c> pattern expression, carrying the negation's trivia.</returns>
    private static IsPatternExpressionSyntax Rewrite(PrefixUnaryExpressionSyntax negation)
    {
        var isExpression = (BinaryExpressionSyntax)ExpressionShapes.WalkDownParentheses(negation.Operand);
        return PatternMatchingAnalyzer.BuildIsNotPattern(isExpression.Left, (TypeSyntax)isExpression.Right).WithTriviaFrom(negation);
    }
}
