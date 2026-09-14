// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an <c>as</c> cast compared to null as an <c>is</c> type pattern (SST2005).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IsPatternOverAsNullCheckCodeFixProvider))]
[Shared]
public sealed class IsPatternOverAsNullCheckCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(Resolve, static (current, _) => Rewrite((BinaryExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseIsPatternOverAsNullCheck.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Use an 'is' type pattern",
            nameof(IsPatternOverAsNullCheckCodeFixProvider),
            Resolve,
            Rewrite);

    /// <summary>Resolves the reported comparison when it still compares an <c>as</c> cast to null.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The comparison, or <see langword="null"/> when the shape no longer matches.</returns>
    private static BinaryExpressionSyntax? Resolve(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is BinaryExpressionSyntax comparison
            && PatternMatchingAnalyzer.GetAsOperandComparedToNull(comparison) is not null
            ? comparison
            : null;

    /// <summary>Builds <c>x is T</c> (for <c>!=</c>) or <c>x is not T</c> (for <c>==</c>) from an <c>as</c>-to-null comparison.</summary>
    /// <param name="comparison">The comparison <see cref="Resolve"/> matched.</param>
    /// <returns>The type-pattern expression, carrying the comparison's trivia.</returns>
    private static ExpressionSyntax Rewrite(BinaryExpressionSyntax comparison)
    {
        var asExpression = PatternMatchingAnalyzer.GetAsOperandComparedToNull(comparison)!;
        var operand = asExpression.Left;
        var type = (TypeSyntax)asExpression.Right;

        ExpressionSyntax replacement = comparison.IsKind(SyntaxKind.NotEqualsExpression)
            ? PatternMatchingAnalyzer.BuildIsTypeTest(operand, type)
            : PatternMatchingAnalyzer.BuildIsNotPattern(operand, type);

        return replacement.WithTriviaFrom(comparison);
    }
}
