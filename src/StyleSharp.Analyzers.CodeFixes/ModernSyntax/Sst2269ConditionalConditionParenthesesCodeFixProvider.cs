// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds or removes the parentheses around a conditional expression's condition (SST2269), following the
/// reported condition's current shape: a parenthesized single token is unwrapped, and an unparenthesized
/// condition is wrapped. The condition expression and the surrounding trivia are preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2269ConditionalConditionParenthesesCodeFixProvider))]
[Shared]
public sealed class Sst2269ConditionalConditionParenthesesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.NormalizeConditionalConditionParentheses.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Normalize the condition parentheses", nameof(Sst2269ConditionalConditionParenthesesCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported condition and flips its parentheses.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not ExpressionSyntax found
            || found.Parent is not ConditionalExpressionSyntax conditional
            || conditional.Condition != found)
        {
            return null;
        }

        if (found is ParenthesizedExpressionSyntax parenthesized && Sst2269ConditionalConditionParenthesesAnalyzer.IsSingleSimpleToken(parenthesized.Expression))
        {
            return new NodeReplacement(parenthesized, parenthesized.Expression.WithTriviaFrom(parenthesized));
        }

        if (found is ParenthesizedExpressionSyntax)
        {
            return null;
        }

        var wrapped = SyntaxFactory.ParenthesizedExpression(found.WithoutTrivia()).WithTriviaFrom(found);
        return new NodeReplacement(found, wrapped);
    }
}
