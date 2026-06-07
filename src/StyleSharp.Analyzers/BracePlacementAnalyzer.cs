// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Inspects brace pairs once and reports the brace-placement layout rules:
/// SST1500 (a brace shares a line with other code in a multi-line construct),
/// SST1505 (an opening brace is followed by a blank line), SST1508 (a closing
/// brace is preceded by a blank line), and SST1509 (an opening brace is preceded
/// by a blank line). A single pass over each brace-bearing node serves all four,
/// keeping the no-diagnostic path allocation-free.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BracePlacementAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        LayoutRules.BracesOnOwnLine,
        LayoutRules.OpenBraceNotFollowedByBlankLine,
        LayoutRules.CloseBraceNotPrecededByBlankLine,
        LayoutRules.OpenBraceNotPrecededByBlankLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, LayoutHelpers.BraceBearingKinds());
    }

    /// <summary>Checks the brace pair of a single node against the brace-placement rules.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!LayoutHelpers.TryGetBraces(context.Node, out var open, out var close))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var openLine = LayoutHelpers.StartLine(text, open);
        var closeLine = LayoutHelpers.StartLine(text, close);
        var openFacts = LayoutHelpers.GetTokenLineFacts(text, open, openLine);

        // Only a brace that starts its own line can be "preceded by a blank line"; a brace that
        // shares its line with earlier code (e.g. an auto-property's '{') is not, even when the
        // member itself is separated from the previous member by a blank line.
        if (LayoutHelpers.IsBlankLine(text, openLine - 1) && openFacts.StartsLine)
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.OpenBraceNotPrecededByBlankLine, open.GetLocation()));
        }

        if (closeLine <= openLine)
        {
            // Single-line block: SST1501/SST1502 own this shape, not the brace-placement rules.
            return;
        }

        if (LayoutHelpers.IsBlankLine(text, openLine + 1))
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.OpenBraceNotFollowedByBlankLine, open.GetLocation()));
        }

        if (LayoutHelpers.IsBlankLine(text, closeLine - 1))
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.CloseBraceNotPrecededByBlankLine, close.GetLocation()));
        }

        if (openFacts.SharesLineWithPrevious || openFacts.SharesLineWithNext)
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BracesOnOwnLine, open.GetLocation()));
        }

        if (!LayoutHelpers.TokenSharesLineWithPrevious(text, close, closeLine))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BracesOnOwnLine, close.GetLocation()));
    }
}
