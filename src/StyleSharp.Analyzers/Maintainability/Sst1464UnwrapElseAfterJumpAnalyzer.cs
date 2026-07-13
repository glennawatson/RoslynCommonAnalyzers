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
/// <remarks>
/// In an <c>else if</c> chain every earlier arm must jump too, because the else's statements can only
/// be hoisted past the whole chain. One arm that falls through makes the <c>else</c> load bearing:
/// <code>
/// if (found) { next = value; }      // falls through
/// else if (TryClaim()) { return; }  // jumps
/// else { continue; }                // not reported
/// </code>
/// Hoisting <c>continue</c> out would put it on the path <c>found</c> takes as well, and the loop
/// would continue where it used to fall through. The <c>else</c> is carrying the chain rather than
/// merely nesting it, so there is no unwrap to make.
/// </remarks>
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
        if (elseClause.Parent is not IfStatementSyntax ifStatement
            || !BranchAlwaysJumps(ifStatement.Statement)
            || !EveryEarlierArmJumps(ifStatement))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.UnwrapElseAfterJump, elseClause.ElseKeyword.GetLocation()));
    }

    /// <summary>Returns whether every arm before this one in an <c>else if</c> chain also jumps.</summary>
    /// <param name="ifStatement">The if statement the else clause belongs to.</param>
    /// <returns><see langword="true"/> when hoisting the else's statements out would keep the same flow.</returns>
    /// <remarks>
    /// The else's statements can only be hoisted past the whole chain, so they must be unreachable from
    /// every earlier arm. One arm that falls through is enough to make the else load bearing. Hoisting the
    /// <c>continue</c> out of an <c>if (found) … else if (TryClaim()) { return; } else { continue; }</c>
    /// whose first arm merely assigns would put it on the path <c>found</c> takes as well, and the loop
    /// would continue where it used to fall through. When every earlier arm jumps, the chain flattens one
    /// clause at a time and each step keeps the flow it had.
    /// </remarks>
    private static bool EveryEarlierArmJumps(IfStatementSyntax ifStatement)
    {
        var current = ifStatement;
        while (current.Parent is ElseClauseSyntax { Parent: IfStatementSyntax outer })
        {
            if (!BranchAlwaysJumps(outer.Statement))
            {
                return false;
            }

            current = outer;
        }

        return true;
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
