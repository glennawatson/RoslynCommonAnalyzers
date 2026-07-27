// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites a test against <c>object</c> as the null check it actually is (SST2019).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2019NullCheckOverTypeCheckCodeFixProvider))]
[Shared]
public sealed class Sst2019NullCheckOverTypeCheckCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.NullCheckOverTypeCheck.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Test for null",
            nameof(Sst2019NullCheckOverTypeCheckCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported test and builds the equivalent null pattern.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        return node switch
        {
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression)
                => new NodeReplacement(binary, BuildNullPattern(binary.Left, negated: true).WithTriviaFrom(binary)),
            IsPatternExpressionSyntax pattern
                => new NodeReplacement(pattern, BuildNullPattern(pattern.Expression, negated: false).WithTriviaFrom(pattern)),
            _ => null,
        };
    }

    /// <summary>Builds <c>operand is null</c> or <c>operand is not null</c>.</summary>
    /// <param name="operand">The tested expression.</param>
    /// <param name="negated">Whether the result should be the non-null test.</param>
    /// <returns>The rewritten pattern expression.</returns>
    private static IsPatternExpressionSyntax BuildNullPattern(ExpressionSyntax operand, bool negated)
    {
        PatternSyntax nullPattern = SyntaxFactory.ConstantPattern(
            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

        if (negated)
        {
            nullPattern = SyntaxFactory.UnaryPattern(
                SyntaxFactory.Token(SyntaxKind.NotKeyword),
                nullPattern.WithLeadingTrivia(SyntaxFactory.Space));
        }

        return SyntaxFactory.IsPatternExpression(operand.WithoutTrailingTrivia(), nullPattern);
    }
}
