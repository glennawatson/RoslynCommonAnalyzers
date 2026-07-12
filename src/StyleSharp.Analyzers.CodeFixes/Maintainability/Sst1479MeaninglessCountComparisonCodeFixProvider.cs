// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a comparison that a count can never fail with the constant it always evaluates to (SST1479).
/// </summary>
/// <remarks>
/// The fix stops at the comparison. Collapsing the <c>if</c> that wraps it is a judgement about what the
/// author meant — the guard is usually salvageable, not deletable — so the constant is left in place for them
/// to read and finish.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1479MeaninglessCountComparisonCodeFixProvider))]
[Shared]
public sealed class Sst1479MeaninglessCountComparisonCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.MeaninglessCountComparison.Id);

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
            if (!TryGetComparison(root, diagnostic, out var binary, out var result))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    result ? "Replace the comparison with 'true'" : "Replace the comparison with 'false'",
                    _ => Task.FromResult(Apply(context.Document, root, binary!, result)),
                    equivalenceKey: nameof(Sst1479MeaninglessCountComparisonCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetComparison(editor.OriginalRoot, diagnostic, out var binary, out var result))
        {
            return;
        }

        editor.ReplaceNode(binary!, (current, _) => BuildLiteral(current, result));
    }

    /// <summary>Replaces the comparison with the constant it folds to.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="binary">The reported comparison.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax binary, bool result)
        => document.WithSyntaxRoot(root.ReplaceNode(binary, BuildLiteral(binary, result)));

    /// <summary>Resolves the diagnostic's span back to the comparison, and re-derives its constant.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="binary">The reported comparison when found.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <returns><see langword="true"/> when the reported shape still folds.</returns>
    /// <remarks>The fold reads only the operator and the literal's sign, so the fix confirms it without binding.</remarks>
    private static bool TryGetComparison(SyntaxNode root, Diagnostic diagnostic, out BinaryExpressionSyntax? binary, out bool result)
    {
        result = false;
        binary = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as BinaryExpressionSyntax;
        return binary is not null && Sst1479MeaninglessCountComparisonAnalyzer.TryGetConstantResult(binary, out result);
    }

    /// <summary>Builds the boolean literal that takes the comparison's place, keeping its trivia.</summary>
    /// <param name="node">The comparison being replaced, including any nested batch edits.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <returns>The <c>true</c> or <c>false</c> literal.</returns>
    private static LiteralExpressionSyntax BuildLiteral(SyntaxNode node, bool result)
        => SyntaxFactory.LiteralExpression(result ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression)
            .WithTriviaFrom(node);
}
