// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Reflows a wrapped conditional so its operators lead the branch lines.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1140ConditionalOperatorIndentationCodeFixProvider))]
[Shared]
public sealed class Sst1140ConditionalOperatorIndentationCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ConditionalOperatorIndentedLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            static (text, root, diagnostic) => CanBuildChanges(text, root, diagnostic) ? "Reflow conditional operators" : null,
            nameof(Sst1140ConditionalOperatorIndentationCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryFindConditional(root, diagnostic, out var conditional))
        {
            return;
        }

        AppendChanges(text, conditional, changes);
    }

    /// <summary>Returns whether the diagnostic can be fixed without touching non-whitespace trivia.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to inspect.</param>
    /// <returns><see langword="true"/> when a safe text rewrite exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanBuildChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic) =>
        TryFindConditional(root, diagnostic, out var conditional) && CanReplaceOperatorGaps(text, conditional);

    /// <summary>Appends the text changes that move both operators to the expected branch-leading indentation.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="conditional">The conditional expression to reflow.</param>
    /// <param name="changes">The change list to append to.</param>
    private static void AppendChanges(SourceText text, ConditionalExpressionSyntax conditional, List<TextChange> changes)
    {
        if (!CanReplaceOperatorGaps(text, conditional))
        {
            return;
        }

        BuildChanges(text, conditional, out var question, out var colon);
        changes.Add(question);
        changes.Add(colon);
    }

    /// <summary>Returns whether both operators and their surrounding trivia can be replaced by plain layout trivia.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="conditional">The conditional expression to reflow.</param>
    /// <returns><see langword="true"/> when neither gap holds a comment or other trivia.</returns>
    private static bool CanReplaceOperatorGaps(SourceText text, ConditionalExpressionSyntax conditional) =>
        CanReplaceOperatorGap(text, conditional.Condition.GetLastToken(), conditional.QuestionToken, conditional.WhenTrue.GetFirstToken())
            && CanReplaceOperatorGap(text, conditional.WhenTrue.GetLastToken(), conditional.ColonToken, conditional.WhenFalse.GetFirstToken());

    /// <summary>Builds the changes that put each operator at the head of its branch line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="conditional">The conditional expression to reflow.</param>
    /// <param name="question">The change that rewrites the gap around <c>?</c>.</param>
    /// <param name="colon">The change that rewrites the gap around <c>:</c>.</param>
    private static void BuildChanges(SourceText text, ConditionalExpressionSyntax conditional, out TextChange question, out TextChange colon)
    {
        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var indent = LayoutFixHelpers.IndentOfLine(text, conditional.GetFirstToken().SpanStart) + LayoutFixHelpers.IndentStep;
        question = new(TextSpan.FromBounds(conditional.Condition.GetLastToken().Span.End, conditional.WhenTrue.GetFirstToken().SpanStart), $"{newLine}{indent}? ");
        colon = new(TextSpan.FromBounds(conditional.WhenTrue.GetLastToken().Span.End, conditional.WhenFalse.GetFirstToken().SpanStart), $"{newLine}{indent}: ");
    }

    /// <summary>Returns whether an operator and its surrounding trivia can be replaced by plain layout trivia.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="previous">The token before the operator.</param>
    /// <param name="operatorToken">The conditional operator token.</param>
    /// <param name="next">The token after the operator.</param>
    /// <returns><see langword="true"/> when no comments or other trivia would be removed.</returns>
    private static bool CanReplaceOperatorGap(SourceText text, SyntaxToken previous, SyntaxToken operatorToken, SyntaxToken next) =>
        LayoutFixHelpers.IsWhitespaceBetween(text, previous.Span.End, operatorToken.SpanStart)
            && LayoutFixHelpers.IsWhitespaceBetween(text, operatorToken.Span.End, next.SpanStart);

    /// <summary>Finds the conditional expression containing the reported operator token.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to locate.</param>
    /// <param name="conditional">The containing conditional expression.</param>
    /// <returns><see langword="true"/> when a conditional expression was found.</returns>
    private static bool TryFindConditional(SyntaxNode root, Diagnostic diagnostic, out ConditionalExpressionSyntax conditional)
    {
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        for (var node = token.Parent; node is not null; node = node.Parent)
        {
            if (node is not ConditionalExpressionSyntax current)
            {
                continue;
            }

            conditional = current;
            return true;
        }

        conditional = null!;
        return false;
    }
}
