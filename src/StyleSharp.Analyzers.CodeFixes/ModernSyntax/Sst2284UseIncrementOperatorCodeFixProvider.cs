// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a step-by-one compound assignment as the matching stepping operator (SST2284):
/// <c>i += 1;</c> becomes <c>i++;</c> and <c>i -= 1;</c> becomes <c>i--;</c>. The postfix form is used
/// because the statement discards the value, so prefix and postfix are interchangeable there.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2284UseIncrementOperatorCodeFixProvider))]
[Shared]
public sealed class Sst2284UseIncrementOperatorCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(ModernSyntaxRules.UseIncrementOperator.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use the stepping operator",
            nameof(Sst2284UseIncrementOperatorCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported assignment and replaces it with the postfix stepping form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<AssignmentExpressionSyntax>() is not { } assignment)
        {
            return null;
        }

        var increment = assignment.IsKind(SyntaxKind.AddAssignmentExpression);
        if (!increment && !assignment.IsKind(SyntaxKind.SubtractAssignmentExpression))
        {
            return null;
        }

        var replacement = SyntaxFactory.PostfixUnaryExpression(
                increment ? SyntaxKind.PostIncrementExpression : SyntaxKind.PostDecrementExpression,
                assignment.Left.WithoutTrivia(),
                SyntaxFactory.Token(increment ? SyntaxKind.PlusPlusToken : SyntaxKind.MinusMinusToken))
            .WithTriviaFrom(assignment);

        return new NodeReplacement(assignment, replacement);
    }
}
