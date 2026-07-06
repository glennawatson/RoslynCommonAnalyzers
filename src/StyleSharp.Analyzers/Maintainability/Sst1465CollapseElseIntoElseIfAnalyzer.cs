// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>else</c> clause whose block contains exactly one statement that is itself an
/// <c>if</c> statement (SST1465). Such a block only adds a level of nesting; the branch reads the
/// same flattened into an <c>else if</c> chain. Brace-free <c>else if</c> clauses, blocks with more
/// than one statement, and blocks wrapping any other statement kind are left alone.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1465CollapseElseIntoElseIfAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.CollapseElseIntoElseIf);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ElseClause);
    }

    /// <summary>Reports else clauses whose block only wraps an if statement.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var elseClause = (ElseClauseSyntax)context.Node;
        if (elseClause.Statement is not BlockSyntax block
            || block.Statements.Count != 1
            || block.Statements[0] is not IfStatementSyntax)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.CollapseElseIntoElseIf, elseClause.ElseKeyword.GetLocation()));
    }
}
