// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a blank line before a chained statement keyword: an <c>else</c>, <c>catch</c>,
/// or <c>finally</c> that is separated from the preceding block (SST1510), and the
/// <c>while</c> footer of a do/while loop (SST1511). These keywords follow the block they
/// chain to directly.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ChainedBlockSpacingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The clause kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ElseClause,
        SyntaxKind.CatchClause,
        SyntaxKind.FinallyClause,
        SyntaxKind.DoStatement);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        LayoutRules.ChainedBlockNotPrecededByBlankLine,
        LayoutRules.WhileFooterNotPrecededByBlankLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports a blank line between the chained keyword and the block it follows.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var keyword = Keyword(context.Node);
        var previous = keyword.GetPreviousToken();
        if (previous.IsKind(SyntaxKind.None))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LayoutHelpers.StartLine(text, keyword) <= LayoutHelpers.EndLine(text, previous) + 1)
        {
            return;
        }

        if (context.Node is DoStatementSyntax)
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.WhileFooterNotPrecededByBlankLine, keyword.GetLocation()));
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.ChainedBlockNotPrecededByBlankLine, keyword.GetLocation(), keyword.ValueText));
    }

    /// <summary>Returns the leading keyword of a chained clause or do/while footer.</summary>
    /// <param name="node">The clause or statement node.</param>
    /// <returns>The keyword token whose preceding blank line is checked.</returns>
    private static SyntaxToken Keyword(SyntaxNode node) => node switch
    {
        ElseClauseSyntax @else => @else.ElseKeyword,
        CatchClauseSyntax @catch => @catch.CatchKeyword,
        FinallyClauseSyntax @finally => @finally.FinallyKeyword,
        DoStatementSyntax @do => @do.WhileKeyword,
        _ => default
    };
}
