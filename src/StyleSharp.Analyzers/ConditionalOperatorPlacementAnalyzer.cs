// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a wrapped conditional expression whose <c>?</c> or <c>:</c> operator is on the wrong end
/// of a line for the configured placement (SST1145). The default — and the dominant .NET style — is
/// <c>leading</c>: a wrapped operator begins its continuation line. Set
/// <c>stylesharp.conditional_operator_placement</c> to <c>trailing</c> to invert it. Only operators
/// that are actually wrapped are checked, so single-line conditionals are never reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConditionalOperatorPlacementAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Rule-specific editorconfig key for the operator placement (SST1145).</summary>
    internal const string PlacementSpecificKey = "stylesharp.SST1145.conditional_operator_placement";

    /// <summary>General editorconfig key for the operator placement.</summary>
    internal const string PlacementGeneralKey = "stylesharp.conditional_operator_placement";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.ConditionalOperatorPlacement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
    }

    /// <summary>Reports SST1145 for each wrapped conditional operator on the wrong end of its line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        var breakBeforeQuestion = HasLineBreakBefore(conditional.Condition.GetLastToken(), conditional.QuestionToken);
        var breakAfterQuestion = HasLineBreakAfter(conditional.QuestionToken, conditional.WhenTrue.GetFirstToken());
        var breakBeforeColon = HasLineBreakBefore(conditional.WhenTrue.GetLastToken(), conditional.ColonToken);
        var breakAfterColon = HasLineBreakAfter(conditional.ColonToken, conditional.WhenFalse.GetFirstToken());
        if (!breakBeforeQuestion && !breakAfterQuestion && !breakBeforeColon && !breakAfterColon)
        {
            return;
        }

        var leading = ReadLeadingPlacement(context);
        CheckOperator(context, conditional.QuestionToken, breakBeforeQuestion, breakAfterQuestion, leading);
        CheckOperator(context, conditional.ColonToken, breakBeforeColon, breakAfterColon, leading);
    }

    /// <summary>Reports an operator that wraps onto the wrong end of its line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="operatorToken">The <c>?</c> or <c>:</c> token.</param>
    /// <param name="breakBefore">Whether the operator starts on a new line relative to the previous token.</param>
    /// <param name="breakAfter">Whether the following token starts on a new line relative to the operator.</param>
    /// <param name="leading">Whether the operator should begin its line.</param>
    private static void CheckOperator(
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
