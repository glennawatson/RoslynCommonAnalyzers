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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.ArrowTokenNewLine);

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
        if (!breakBefore && !breakAfter)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(clause.SyntaxTree);
        var wantBreakBefore = LayoutStyleOptions.ReadBreakBefore(options, SpecificKey, GeneralKey, defaultBreakBefore: false);
        if (wantBreakBefore ? !breakAfter : !breakBefore)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            LayoutRules.ArrowTokenNewLine,
            arrow.GetLocation(),
            LayoutHelpers.PlacementProperties(wantBreakBefore),
            wantBreakBefore ? "start" : "end"));
    }
}
