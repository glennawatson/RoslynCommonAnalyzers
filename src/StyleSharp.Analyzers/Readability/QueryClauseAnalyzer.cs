// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the layout rules for the clauses of a LINQ query expression (SST1102–SST1105): clauses
/// should follow one another without blank lines, be either all on one line or each on its own line,
/// and a clause that spans multiple lines should begin on its own line. The query's clause list is
/// walked once and each adjacent pair examined, so the whole family shares one pass. Continuation
/// (<c>into</c>) segments are not inspected.
/// </summary>
/// <remarks>
/// Diagnostics: SST1102, SST1103, SST1104, SST1105.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryClauseAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.QueryClauseFollowsPrevious,
        ReadabilityRules.QueryClausesConsistentLines,
        ReadabilityRules.QueryClauseOnNewLineAfterMultiLine,
        ReadabilityRules.QueryClauseMultiLineOwnLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.QueryExpression);
    }

    /// <summary>Reports query-clause layout violations for a query expression.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var query = (QueryExpressionSyntax)context.Node;
        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var allOnOneLine = LineOf(text, query.SpanStart) == LineOf(text, query.Span.End);

        // A query always runs from-clause → body clauses → select/group; track the previous clause
        // through the body's struct enumerator rather than collecting the clauses into a list. A
        // nested query is analysed when its own QueryExpression node is visited.
        SyntaxNode previous = query.FromClause;
        foreach (var clause in query.Body.Clauses)
        {
            Examine(context, text, previous, clause, allOnOneLine);
            previous = clause;
        }

        Examine(context, text, previous, query.Body.SelectOrGroup, allOnOneLine);
    }

    /// <summary>Examines one adjacent clause pair and reports at most one layout violation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="previous">The earlier clause.</param>
    /// <param name="current">The later clause.</param>
    /// <param name="allOnOneLine">Whether the whole query sits on a single line.</param>
    private static void Examine(SyntaxNodeAnalysisContext context, SourceText text, SyntaxNode previous, SyntaxNode current, bool allOnOneLine)
    {
        var previousStart = LineOf(text, previous.SpanStart);
        var previousEnd = LineOf(text, previous.Span.End);
        var currentStart = LineOf(text, current.SpanStart);
        var currentEnd = LineOf(text, current.Span.End);

        if (currentStart > previousEnd + 1)
        {
            Report(context, ReadabilityRules.QueryClauseFollowsPrevious, current);
            return;
        }

        if (currentStart != previousEnd)
        {
            return;
        }

        // The current clause shares the previous clause's last line; choose the most specific rule.
        if (previousEnd > previousStart)
        {
            Report(context, ReadabilityRules.QueryClauseOnNewLineAfterMultiLine, current);
        }
        else if (currentEnd > currentStart)
        {
            Report(context, ReadabilityRules.QueryClauseMultiLineOwnLine, current);
        }
        else if (!allOnOneLine)
        {
            Report(context, ReadabilityRules.QueryClausesConsistentLines, current);
        }
    }

    /// <summary>Reports a diagnostic on a query clause.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="rule">The descriptor to report.</param>
    /// <param name="clause">The clause to flag.</param>
    private static void Report(SyntaxNodeAnalysisContext context, DiagnosticDescriptor rule, SyntaxNode clause)
        => context.ReportDiagnostic(Diagnostic.Create(rule, clause.GetLocation()));

    /// <summary>Returns the zero-based line number for a position.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The position to look up.</param>
    /// <returns>The line number.</returns>
    private static int LineOf(SourceText text, int position) => text.Lines.GetLineFromPosition(position).LineNumber;
}
