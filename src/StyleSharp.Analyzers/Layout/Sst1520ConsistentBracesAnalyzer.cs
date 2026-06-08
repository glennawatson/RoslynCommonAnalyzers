// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an if/else chain whose clauses use braces inconsistently (SST1520) — some
/// clauses wrapped in braces and others not. The whole chain is examined once from its top.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1520ConsistentBracesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.BracesUsedConsistently);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
    }

    /// <summary>Reports the top of an if/else chain when its clauses mix braced and unbraced bodies.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (ifStatement.Parent is ElseClauseSyntax)
        {
            return;
        }

        var sawBlock = false;
        var sawNonBlock = false;
        var current = ifStatement;
        while (true)
        {
            Classify(current.Statement, ref sawBlock, ref sawNonBlock);
            if (current.Else is not { } elseClause)
            {
                break;
            }

            if (elseClause.Statement is IfStatementSyntax elseIf)
            {
                current = elseIf;
                continue;
            }

            Classify(elseClause.Statement, ref sawBlock, ref sawNonBlock);
            break;
        }

        if (!sawBlock || !sawNonBlock)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BracesUsedConsistently, ifStatement.IfKeyword.GetLocation()));
    }

    /// <summary>Records whether a clause body is a braced block or a bare statement.</summary>
    /// <param name="statement">The clause body.</param>
    /// <param name="sawBlock">Set when the body is a block.</param>
    /// <param name="sawNonBlock">Set when the body is a bare statement.</param>
    private static void Classify(StatementSyntax statement, ref bool sawBlock, ref bool sawNonBlock)
    {
        if (statement is BlockSyntax)
        {
            sawBlock = true;
        }
        else
        {
            sawNonBlock = true;
        }
    }
}
