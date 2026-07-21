// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a <c>ReferenceEquals</c> null check as a null pattern (SST2282):
/// <c>ReferenceEquals(value, null)</c> becomes <c>value is null</c>, and the negated
/// <c>!ReferenceEquals(value, null)</c> becomes <c>value is not null</c>. The non-null operand is
/// parenthesized only when it is not already a primary expression.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2282ReferenceEqualsNullPatternCodeFixProvider))]
[Shared]
public sealed class Sst2282ReferenceEqualsNullPatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(ModernSyntaxRules.UseNullPatternOverReferenceEquals.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Match null with an is-pattern", nameof(Sst2282ReferenceEqualsNullPatternCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported call and rewrites it (or its enclosing <c>!</c>) as a null pattern.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
            || !Sst2282ReferenceEqualsNullPatternAnalyzer.TryGetNonNullOperand(invocation, out var nonNullOperand))
        {
            return null;
        }

        var negation = Sst2282ReferenceEqualsNullPatternAnalyzer.GetEnclosingLogicalNot(invocation);
        var replaced = (ExpressionSyntax?)negation ?? invocation;

        var operand = ExpressionSimplificationAnalyzer.Unwrap(nonNullOperand).WithoutTrivia();
        var subject = PrimaryExpressionClassification.IsPrimary(operand)
            ? operand
            : SyntaxFactory.ParenthesizedExpression(operand);

        PatternSyntax nullPattern = SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
        if (negation is not null)
        {
            nullPattern = SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword), nullPattern);
        }

        var isPattern = SyntaxFactory.IsPatternExpression(subject, nullPattern).WithTriviaFrom(replaced);
        return new NodeReplacement(replaced, isPattern);
    }
}
