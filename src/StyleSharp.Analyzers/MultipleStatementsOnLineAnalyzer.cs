// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a statement that begins on the same line a previous statement ended (SST1107). Each
/// statement belongs on its own line so the control flow reads top-to-bottom. Empty statements
/// are governed by SST1106 and skipped here to avoid a duplicate report.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultipleStatementsOnLineAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.MultipleStatementsOnLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchSection, SyntaxKind.SwitchSection);
    }

    /// <summary>Reports same-line statements within a block.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeBlock(SyntaxNodeAnalysisContext context)
        => Analyze(context, ((BlockSyntax)context.Node).Statements);

    /// <summary>Reports same-line statements within a switch section.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeSwitchSection(SyntaxNodeAnalysisContext context)
        => Analyze(context, ((SwitchSectionSyntax)context.Node).Statements);

    /// <summary>Reports each statement that starts on the line the previous statement ended.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="statements">The statement list to inspect.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, SyntaxList<StatementSyntax> statements)
    {
        if (statements.Count < 2)
        {
            return;
        }

        for (var index = 1; index < statements.Count; index++)
        {
            var previous = statements[index - 1];
            var statement = statements[index];
            if (statement is not EmptyStatementSyntax && AreOnSameLine(previous, statement))
            {
                context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.MultipleStatementsOnLine, statement.GetLocation()));
            }
        }
    }

    /// <summary>Returns whether two consecutive statements are separated without a line break.</summary>
    /// <param name="previous">The earlier statement.</param>
    /// <param name="current">The later statement.</param>
    /// <returns><see langword="true"/> when both statements share a line.</returns>
    private static bool AreOnSameLine(StatementSyntax previous, StatementSyntax current)
        => !TriviaLineBreakHelper.HasLineBreak(previous.GetLastToken().TrailingTrivia)
            && !TriviaLineBreakHelper.HasLineBreak(current.GetFirstToken().LeadingTrivia);
}
