// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

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

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var previousEndLine = text.Lines.GetLineFromPosition(statements[0].Span.End).LineNumber;
        for (var index = 1; index < statements.Count; index++)
        {
            var statement = statements[index];
            var startLine = text.Lines.GetLineFromPosition(statement.SpanStart).LineNumber;
            if (startLine == previousEndLine && statement is not EmptyStatementSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.MultipleStatementsOnLine, statement.GetLocation()));
            }

            previousEndLine = text.Lines.GetLineFromPosition(statement.Span.End).LineNumber;
        }
    }
}
