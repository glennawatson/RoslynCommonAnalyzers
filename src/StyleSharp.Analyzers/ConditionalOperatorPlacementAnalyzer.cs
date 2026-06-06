// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

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
        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LineOf(text, conditional.SpanStart) == LineOf(text, conditional.Span.End))
        {
            return;
        }

        var leading = ReadLeadingPlacement(context);
        CheckOperator(context, text, conditional.QuestionToken, conditional.Condition.GetLastToken(), conditional.WhenTrue.GetFirstToken(), leading);
        CheckOperator(context, text, conditional.ColonToken, conditional.WhenTrue.GetLastToken(), conditional.WhenFalse.GetFirstToken(), leading);
    }

    /// <summary>Reports an operator that wraps onto the wrong end of its line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="operatorToken">The <c>?</c> or <c>:</c> token.</param>
    /// <param name="previous">The token preceding the operator.</param>
    /// <param name="next">The token following the operator.</param>
    /// <param name="leading">Whether the operator should begin its line.</param>
    private static void CheckOperator(SyntaxNodeAnalysisContext context, SourceText text, SyntaxToken operatorToken, SyntaxToken previous, SyntaxToken next, bool leading)
    {
        var breakBefore = LineOf(text, operatorToken.SpanStart) > LineOf(text, previous.Span.End);
        var breakAfter = LineOf(text, next.SpanStart) > LineOf(text, operatorToken.Span.End);

        if (leading && breakAfter)
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ConditionalOperatorPlacement, operatorToken.GetLocation(), operatorToken.Text, "start"));
        }
        else if (!leading && breakBefore)
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ConditionalOperatorPlacement, operatorToken.GetLocation(), operatorToken.Text, "end"));
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

    /// <summary>Returns the zero-based line number containing a position.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The character position.</param>
    /// <returns>The line number.</returns>
    private static int LineOf(SourceText text, int position) => text.Lines.GetLinePosition(position).Line;
}
