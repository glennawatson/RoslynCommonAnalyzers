// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a redundant null check that sits beside an <c>is</c> type pattern (SST2018), collapsing the
/// expression to the pattern test alone.
/// </summary>
/// <remarks>The fix keeps the existing pattern node, so it never introduces syntax the compilation does not already use.</remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2018RedundantNullCheckBesidePatternCodeFixProvider))]
[Shared]
public sealed class Sst2018RedundantNullCheckBesidePatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.RedundantNullCheckBesidePattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the redundant null check",
            nameof(Sst2018RedundantNullCheckBesidePatternCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported expression and reduces it to the pattern test.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is BinaryExpressionSyntax binary && (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression)))
        {
            return new NodeReplacement(binary, binary.Right.WithTriviaFrom(binary));
        }

        if (node is not IsPatternExpressionSyntax { Pattern: BinaryPatternSyntax { RawKind: (int)SyntaxKind.AndPattern } and } isPattern
            || TypeArm(and) is not { } typePattern)
        {
            return null;
        }

        // A bare type pattern collapses to an idiomatic 'o is T' type check; a declaration pattern
        // (which binds a variable) stays an is-pattern expression.
        if (typePattern is TypePatternSyntax type)
        {
            var isExpression = SyntaxFactory.BinaryExpression(
                SyntaxKind.IsExpression,
                isPattern.Expression.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.IsKeyword).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
                type.Type.WithoutTrivia());
            return new NodeReplacement(isPattern, isExpression.WithTriviaFrom(isPattern));
        }

        return new NodeReplacement(isPattern, isPattern.WithPattern(typePattern.WithTriviaFrom(isPattern.Pattern)));
    }

    /// <summary>Gets the positive type-pattern arm of an <c>and</c> pattern.</summary>
    /// <param name="and">The <c>and</c> pattern.</param>
    /// <returns>The type pattern arm, or <see langword="null"/> when neither arm is one.</returns>
    private static PatternSyntax? TypeArm(BinaryPatternSyntax and)
    {
        if (and.Left is TypePatternSyntax or DeclarationPatternSyntax)
        {
            return and.Left;
        }

        return and.Right is TypePatternSyntax or DeclarationPatternSyntax ? and.Right : null;
    }
}
