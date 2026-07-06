// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an else clause whose paired if branch can never fall through into it because that
/// branch is, or is a block whose last statement is, a return, throw, continue, or break
/// statement (SST1464). The check is purely syntactic: only the branch's direct last statement
/// is inspected, with no flow analysis.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1464UnwrapElseAfterJumpAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UnwrapElseAfterJump);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ElseClause);
    }

    /// <summary>Returns whether an if branch always jumps instead of falling through to its else clause.</summary>
    /// <param name="statement">The if statement's true branch.</param>
    /// <returns><see langword="true"/> when the branch is, or ends in, a jump statement.</returns>
    internal static bool BranchAlwaysJumps(StatementSyntax statement)
    {
        if (statement is BlockSyntax block)
        {
            var statements = block.Statements;
            return statements.Count > 0 && IsJumpStatement(statements[statements.Count - 1]);
        }

        return IsJumpStatement(statement);
    }

    /// <summary>Reports the else keyword when the paired if branch always jumps.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var elseClause = (ElseClauseSyntax)context.Node;
        if (elseClause.Parent is not IfStatementSyntax ifStatement || !BranchAlwaysJumps(ifStatement.Statement))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.UnwrapElseAfterJump, elseClause.ElseKeyword.GetLocation()));
    }

    /// <summary>Returns whether a statement is a direct jump out of its enclosing flow.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <returns><see langword="true"/> for return, throw, continue, and break statements.</returns>
    private static bool IsJumpStatement(StatementSyntax statement)
        => statement.Kind() is SyntaxKind.ReturnStatement
            or SyntaxKind.ThrowStatement
            or SyntaxKind.ContinueStatement
            or SyntaxKind.BreakStatement;
}
