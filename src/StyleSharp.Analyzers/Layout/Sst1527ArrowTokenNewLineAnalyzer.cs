// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an expression-bodied member whose <c>=&gt;</c> wraps onto the wrong side of its line break
/// (SST1527), configured with <c>stylesharp.arrow_token_new_line</c> (<c>after</c> | <c>before</c>;
/// default <c>after</c>). Only an arrow with an adjacent line break is checked, so a single-line
/// expression body is never touched.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1527ArrowTokenNewLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Rule-specific editorconfig key for the arrow placement (SST1527).</summary>
    internal const string SpecificKey = "stylesharp.SST1527.arrow_token_new_line";

    /// <summary>General editorconfig key for the arrow placement.</summary>
    internal const string GeneralKey = "stylesharp.arrow_token_new_line";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(LayoutRules.ArrowTokenNewLine);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ArrowExpressionClause);
    }

    /// <summary>Reports an expression-body arrow on the wrong side of its line break.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var clause = (ArrowExpressionClauseSyntax)context.Node;
        var arrow = clause.ArrowToken;
        var breakBefore = LayoutHelpers.HasLineBreakBefore(arrow);
        var breakAfter = LayoutHelpers.HasLineBreakAfter(arrow);
        if ((!breakBefore && !breakAfter) || SitsAcrossADirective(arrow))
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(clause.SyntaxTree);
        var wantBreakBefore = LayoutStyleOptions.ReadBreakBefore(options, SpecificKey, GeneralKey, defaultBreakBefore: false);
        if (wantBreakBefore ? !breakAfter : !breakBefore)
        {
            return;
        }

        if (!wantBreakBefore && JoinedLineIsTooLong(context, clause, options))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            LayoutRules.ArrowTokenNewLine,
            arrow.GetLocation(),
            LayoutHelpers.PlacementProperties(wantBreakBefore),
            wantBreakBefore ? "start" : "end"));
    }

    /// <summary>Returns whether pulling the arrow up would make the signature's line too long.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="clause">The expression body.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <returns><see langword="true"/> when the joined line would exceed the configured maximum.</returns>
    /// <remarks>
    /// The arrow wraps because the signature is already long. Moving it up joins the two lines, and a
    /// line over the limit is what SST1521 reports — so the tidier arrow would buy a longer line.
    /// </remarks>
    private static bool JoinedLineIsTooLong(SyntaxNodeAnalysisContext context, ArrowExpressionClauseSyntax clause, AnalyzerConfigOptions options)
    {
        var text = clause.SyntaxTree.GetText(context.CancellationToken);
        var arrowLine = text.Lines.GetLineFromPosition(clause.ArrowToken.SpanStart);
        if (arrowLine.LineNumber == 0)
        {
            return false;
        }

        var signature = text.Lines[arrowLine.LineNumber - 1].ToString().TrimEnd();
        var trailing = arrowLine.ToString().Trim();

        // One space joins them, and the arrow already sits at the front of the trailing text.
        return signature.Length + 1 + trailing.Length > SizeLimitOptions.ReadMaxLineLength(options);
    }

    /// <summary>Returns whether a conditional directive sits between the arrow and the signature.</summary>
    /// <param name="arrow">The expression body's arrow token.</param>
    /// <returns><see langword="true"/> when moving the arrow would move it across the directive.</returns>
    /// <remarks>
    /// An expression body written once per <c>#if</c> branch keeps the arrow inside the branch. Moving it
    /// up to the signature would hoist it out of the branch and change which body each one selects.
    /// </remarks>
    private static bool SitsAcrossADirective(SyntaxToken arrow)
    {
        foreach (var trivia in arrow.LeadingTrivia)
        {
            if (trivia.IsDirective)
            {
                return true;
            }
        }

        foreach (var trivia in arrow.GetPreviousToken().TrailingTrivia)
        {
            if (trivia.IsDirective)
            {
                return true;
            }
        }

        return false;
    }
}
