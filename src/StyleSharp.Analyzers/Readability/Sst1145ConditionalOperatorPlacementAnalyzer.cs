// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports wrapped conditional operator layout rules. SST1140 requires branch-leading operators
/// to sit one indent step beyond the owning expression. SST1145 checks whether <c>?</c> and
/// <c>:</c> sit on the configured end of a wrapped line. The default SST1145 placement is
/// <c>leading</c>; set
/// <c>stylesharp.conditional_operator_placement</c> to <c>trailing</c> to invert it. Only operators
/// that are actually wrapped are checked, so single-line conditionals are never reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1145ConditionalOperatorPlacementAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Rule-specific editorconfig key for the operator placement (SST1145).</summary>
    internal const string PlacementSpecificKey = "stylesharp.SST1145.conditional_operator_placement";

    /// <summary>General editorconfig key for the operator placement.</summary>
    internal const string PlacementGeneralKey = "stylesharp.conditional_operator_placement";

    /// <summary>The expected continuation indent width for SST1140.</summary>
    private const int IndentStepWidth = 4;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.ConditionalOperatorIndentedLine,
        ReadabilityRules.ConditionalOperatorPlacement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
    }

    /// <summary>Reports conditional operator line-placement diagnostics.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        if (conditional.QuestionToken.IsMissing || conditional.ColonToken.IsMissing)
        {
            return;
        }

        var lineBreaks = GetLineBreaks(conditional);
        var operatorWrapped = HasAnyLineBreak(lineBreaks);
        if (!operatorWrapped)
        {
            return;
        }

        var tree = conditional.SyntaxTree;
        var expectedIndent = GetConditionLineIndentColumn(tree, conditional, context.CancellationToken) + IndentStepWidth;
        var questionIndentViolation = CheckIndentedOperator(context, tree, conditional.QuestionToken, lineBreaks.QuestionBefore, expectedIndent);
        var colonIndentViolation = CheckIndentedOperator(context, tree, conditional.ColonToken, lineBreaks.ColonBefore, expectedIndent);

        var leading = ReadLeadingPlacement(context);
        CheckPlacementOperatorWhenNotCovered(context, conditional.QuestionToken, lineBreaks.QuestionBefore, lineBreaks.QuestionAfter, leading, questionIndentViolation);
        CheckPlacementOperatorWhenNotCovered(context, conditional.ColonToken, lineBreaks.ColonBefore, lineBreaks.ColonAfter, leading, colonIndentViolation);
    }

    /// <summary>Returns the line-break shape around both conditional operators.</summary>
    /// <param name="conditional">The conditional expression to inspect.</param>
    /// <returns>The detected line-break shape.</returns>
    private static (bool QuestionBefore, bool QuestionAfter, bool ColonBefore, bool ColonAfter) GetLineBreaks(ConditionalExpressionSyntax conditional)
        => new(
            HasLineBreakBefore(conditional.Condition.GetLastToken(), conditional.QuestionToken),
            HasLineBreakAfter(conditional.QuestionToken, conditional.WhenTrue.GetFirstToken()),
            HasLineBreakBefore(conditional.WhenTrue.GetLastToken(), conditional.ColonToken),
            HasLineBreakAfter(conditional.ColonToken, conditional.WhenFalse.GetFirstToken()));

    /// <summary>Returns whether either conditional operator has an adjacent line break.</summary>
    /// <param name="lineBreaks">The line-break shape.</param>
    /// <returns><see langword="true"/> when either operator is wrapped by adjacent trivia.</returns>
    private static bool HasAnyLineBreak((bool QuestionBefore, bool QuestionAfter, bool ColonBefore, bool ColonAfter) lineBreaks)
        => lineBreaks.QuestionBefore || lineBreaks.QuestionAfter || lineBreaks.ColonBefore || lineBreaks.ColonAfter;

    /// <summary>Runs SST1145 only when SST1140 did not already report the same operator.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="operatorToken">The <c>?</c> or <c>:</c> token.</param>
    /// <param name="breakBefore">Whether the operator starts on a new line relative to the previous token.</param>
    /// <param name="breakAfter">Whether the following token starts on a new line relative to the operator.</param>
    /// <param name="leading">Whether the operator should begin its line.</param>
    /// <param name="coveredByIndentRule">Whether SST1140 already reported this operator.</param>
    private static void CheckPlacementOperatorWhenNotCovered(
        SyntaxNodeAnalysisContext context,
        SyntaxToken operatorToken,
        bool breakBefore,
        bool breakAfter,
        bool leading,
        bool coveredByIndentRule)
    {
        if (coveredByIndentRule)
        {
            return;
        }

        CheckPlacementOperator(context, operatorToken, breakBefore, breakAfter, leading);
    }

    /// <summary>Reports an operator that wraps onto the wrong end of its line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="operatorToken">The <c>?</c> or <c>:</c> token.</param>
    /// <param name="breakBefore">Whether the operator starts on a new line relative to the previous token.</param>
    /// <param name="breakAfter">Whether the following token starts on a new line relative to the operator.</param>
    /// <param name="leading">Whether the operator should begin its line.</param>
    private static void CheckPlacementOperator(
        SyntaxNodeAnalysisContext context,
        SyntaxToken operatorToken,
        bool breakBefore,
        bool breakAfter,
        bool leading)
    {
        switch (leading)
        {
            case true when breakAfter:
                {
                    context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ConditionalOperatorPlacement, operatorToken.GetLocation(), operatorToken.Text, "start"));
                    break;
                }

            case false when breakBefore:
                {
                    context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ConditionalOperatorPlacement, operatorToken.GetLocation(), operatorToken.Text, "end"));
                    break;
                }
        }
    }

    /// <summary>Reports SST1140 when an operator is not the first token on its expected continuation line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="tree">The syntax tree.</param>
    /// <param name="operatorToken">The operator token.</param>
    /// <param name="breakBefore">Whether a line break precedes the operator.</param>
    /// <param name="expectedIndent">The expected indentation width.</param>
    /// <returns><see langword="true"/> when SST1140 was reported.</returns>
    private static bool CheckIndentedOperator(
        SyntaxNodeAnalysisContext context,
        SyntaxTree tree,
        SyntaxToken operatorToken,
        bool breakBefore,
        int expectedIndent)
    {
        if (breakBefore && tree.GetLineSpan(operatorToken.Span).StartLinePosition.Character == expectedIndent)
        {
            return false;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ConditionalOperatorIndentedLine, operatorToken.GetLocation(), operatorToken.Text));
        return true;
    }

    /// <summary>Returns the indentation column of the line where the conditional expression starts.</summary>
    /// <param name="tree">The syntax tree.</param>
    /// <param name="conditional">The conditional expression.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The conditional line indentation column.</returns>
    private static int GetConditionLineIndentColumn(
        SyntaxTree tree,
        ConditionalExpressionSyntax conditional,
        CancellationToken cancellationToken)
    {
        var text = tree.GetText(cancellationToken);
        var line = text.Lines.GetLineFromPosition(conditional.GetFirstToken().SpanStart);
        var column = 0;
        for (var position = line.Start; position < line.End; position++)
        {
            if (!char.IsWhiteSpace(text[position]))
            {
                break;
            }

            column++;
        }

        return column;
    }

    /// <summary>Reads whether operators should lead their line, preferring the rule-specific key.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns><see langword="true"/> for leading placement (the default).</returns>
    private static bool ReadLeadingPlacement(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        var hasValue = (options.TryGetValue(PlacementSpecificKey, out var value) && value.Length > 0)
            || (options.TryGetValue(PlacementGeneralKey, out value) && value.Length > 0);
        return !hasValue || !string.Equals(value, "trailing", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns whether a line break separates the previous token from the operator.</summary>
    /// <param name="previous">The token preceding the operator.</param>
    /// <param name="operatorToken">The operator token.</param>
    /// <returns><see langword="true"/> when the operator begins a new line.</returns>
    private static bool HasLineBreakBefore(SyntaxToken previous, SyntaxToken operatorToken)
        => TriviaLineBreakHelper.HasLineBreak(previous.TrailingTrivia)
            || TriviaLineBreakHelper.HasLineBreak(operatorToken.LeadingTrivia);

    /// <summary>Returns whether a line break separates the operator from the following token.</summary>
    /// <param name="operatorToken">The operator token.</param>
    /// <param name="next">The token following the operator.</param>
    /// <returns><see langword="true"/> when the following token begins a new line.</returns>
    private static bool HasLineBreakAfter(SyntaxToken operatorToken, SyntaxToken next)
        => TriviaLineBreakHelper.HasLineBreak(operatorToken.TrailingTrivia)
            || TriviaLineBreakHelper.HasLineBreak(next.LeadingTrivia);
}
