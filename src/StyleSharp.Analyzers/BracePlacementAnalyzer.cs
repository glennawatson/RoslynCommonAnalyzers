// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

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

        // Only a brace that starts its own line can be "preceded by a blank line"; a brace that
        // shares its line with earlier code (e.g. an auto-property's '{') is not, even when the
        // member itself is separated from the previous member by a blank line.
        if (LayoutHelpers.IsBlankLine(text, openLine - 1) && OpenBraceStartsLine(text, open, openLine))
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

        if (OpenBraceSharesLine(text, open, openLine))
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BracesOnOwnLine, open.GetLocation()));
        }

        if (!CloseBraceSharesLine(text, close, closeLine))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BracesOnOwnLine, close.GetLocation()));
    }

    /// <summary>Returns whether the opening brace is the first token on its line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="open">The opening brace.</param>
    /// <param name="openLine">The line the opening brace is on.</param>
    /// <returns><see langword="true"/> when no earlier token shares the brace's line.</returns>
    private static bool OpenBraceStartsLine(SourceText text, SyntaxToken open, int openLine)
    {
        var previous = open.GetPreviousToken();
        return previous.IsKind(SyntaxKind.None) || LayoutHelpers.EndLine(text, previous) < openLine;
    }

    /// <summary>Returns whether the opening brace shares its line with the token before or after it.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="open">The opening brace.</param>
    /// <param name="openLine">The line the opening brace is on.</param>
    /// <returns><see langword="true"/> when the brace is not alone on its line.</returns>
    private static bool OpenBraceSharesLine(SourceText text, SyntaxToken open, int openLine)
    {
        var previous = open.GetPreviousToken();
        if (!previous.IsKind(SyntaxKind.None) && LayoutHelpers.EndLine(text, previous) == openLine)
        {
            return true;
        }

        var next = open.GetNextToken();
        return !next.IsKind(SyntaxKind.None) && LayoutHelpers.StartLine(text, next) == openLine;
    }

    /// <summary>Returns whether the closing brace shares its line with the token before it.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="close">The closing brace.</param>
    /// <param name="closeLine">The line the closing brace is on.</param>
    /// <returns><see langword="true"/> when code precedes the brace on its line.</returns>
    private static bool CloseBraceSharesLine(SourceText text, SyntaxToken close, int closeLine)
    {
        var previous = close.GetPreviousToken();
        return !previous.IsKind(SyntaxKind.None) && LayoutHelpers.EndLine(text, previous) == closeLine;
    }
}
