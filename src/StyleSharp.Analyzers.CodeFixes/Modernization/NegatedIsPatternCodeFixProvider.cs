// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites a negated type test <c>!(x is T)</c> as the <c>x is not T</c> pattern (SST2006).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NegatedIsPatternCodeFixProvider))]
[Shared]
public sealed class NegatedIsPatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseNegatedIsPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not PrefixUnaryExpressionSyntax not
                || PatternMatchingAnalyzer.Unwrap(not.Operand) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpression
                || isExpression.Right is not TypeSyntax type)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the 'is not' pattern",
                    _ => Task.FromResult(Apply(context.Document, root, not, isExpression.Left, type)),
                    equivalenceKey: nameof(NegatedIsPatternCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not PrefixUnaryExpressionSyntax not
            || PatternMatchingAnalyzer.Unwrap(not.Operand) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpression
            || isExpression.Right is not TypeSyntax type)
        {
            return;
        }

        var replacement = PatternMatchingAnalyzer.BuildIsNotPattern(isExpression.Left, type).WithTriviaFrom(not);
        editor.ReplaceNode(not, replacement);
    }

    /// <summary>Replaces the negated type test with the <c>is not</c> pattern.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="not">The logical-not expression.</param>
    /// <param name="operand">The value being tested.</param>
    /// <param name="type">The type being tested for.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, PrefixUnaryExpressionSyntax not, ExpressionSyntax operand, TypeSyntax type)
    {
        var replacement = PatternMatchingAnalyzer.BuildIsNotPattern(operand, type).WithTriviaFrom(not);
        return document.WithSyntaxRoot(root.ReplaceNode(not, replacement));
    }
}
