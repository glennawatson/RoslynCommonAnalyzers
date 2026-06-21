// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Reflows a wrapped conditional so its operators lead the branch lines.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1140ConditionalOperatorIndentationCodeFixProvider))]
[Shared]
public sealed class Sst1140ConditionalOperatorIndentationCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ConditionalOperatorIndentedLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            if (!CanBuildChanges(text, root, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Reflow conditional operators",
                    _ => Task.FromResult(Apply(context.Document, text, root, diagnostic)),
                    equivalenceKey: nameof(Sst1140ConditionalOperatorIndentationCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryFindConditional(root, diagnostic, out var conditional))
        {
            return;
        }

        AppendChanges(text, conditional, changes);
    }

    /// <summary>Applies the text changes for one diagnostic.</summary>
    /// <param name="document">The document to update.</param>
    /// <param name="text">The current source text.</param>
    /// <param name="root">The current syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SourceText text, SyntaxNode root, Diagnostic diagnostic)
    {
        var changes = new List<TextChange>();
        if (!TryFindConditional(root, diagnostic, out var conditional))
        {
            return document;
        }

        AppendChanges(text, conditional, changes);
        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Returns whether the diagnostic can be fixed without touching non-whitespace trivia.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to inspect.</param>
    /// <returns><see langword="true"/> when a safe text rewrite exists.</returns>
    private static bool CanBuildChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryFindConditional(root, diagnostic, out var conditional))
        {
            return false;
        }

        var conditionLast = conditional.Condition.GetLastToken();
        var whenTrueFirst = conditional.WhenTrue.GetFirstToken();
        var whenTrueLast = conditional.WhenTrue.GetLastToken();
        var whenFalseFirst = conditional.WhenFalse.GetFirstToken();
        return CanReplaceOperatorGap(text, conditionLast, conditional.QuestionToken, whenTrueFirst)
            && CanReplaceOperatorGap(text, whenTrueLast, conditional.ColonToken, whenFalseFirst);
    }

    /// <summary>Appends the text changes that move both operators to the expected branch-leading indentation.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="conditional">The conditional expression to reflow.</param>
    /// <param name="changes">The change list to append to.</param>
    private static void AppendChanges(SourceText text, ConditionalExpressionSyntax conditional, List<TextChange> changes)
    {
        var conditionLast = conditional.Condition.GetLastToken();
        var whenTrueFirst = conditional.WhenTrue.GetFirstToken();
        var whenTrueLast = conditional.WhenTrue.GetLastToken();
        var whenFalseFirst = conditional.WhenFalse.GetFirstToken();
        if (!CanReplaceOperatorGap(text, conditionLast, conditional.QuestionToken, whenTrueFirst)
            || !CanReplaceOperatorGap(text, whenTrueLast, conditional.ColonToken, whenFalseFirst))
        {
            return;
        }

        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var indent = LayoutFixHelpers.IndentOfLine(text, conditional.GetFirstToken().SpanStart) + LayoutFixHelpers.IndentStep;
        changes.Add(new(TextSpan.FromBounds(conditionLast.Span.End, whenTrueFirst.SpanStart), newLine + indent + "? "));
        changes.Add(new(TextSpan.FromBounds(whenTrueLast.Span.End, whenFalseFirst.SpanStart), newLine + indent + ": "));
    }

    /// <summary>Returns whether an operator and its surrounding trivia can be replaced by plain layout trivia.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="previous">The token before the operator.</param>
    /// <param name="operatorToken">The conditional operator token.</param>
    /// <param name="next">The token after the operator.</param>
    /// <returns><see langword="true"/> when no comments or other trivia would be removed.</returns>
    private static bool CanReplaceOperatorGap(SourceText text, SyntaxToken previous, SyntaxToken operatorToken, SyntaxToken next)
        => LayoutFixHelpers.IsWhitespaceBetween(text, previous.Span.End, operatorToken.SpanStart)
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
            if (node is ConditionalExpressionSyntax current)
            {
                conditional = current;
                return true;
            }
        }

        conditional = null!;
        return false;
    }
}
