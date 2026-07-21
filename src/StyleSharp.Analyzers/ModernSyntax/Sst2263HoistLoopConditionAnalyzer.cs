// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an infinite loop whose body carries a guard condition that belongs in the loop header (SST2263):
/// <c>while (true) { if (cond) { work } else break; }</c> and the guard-then-break shape
/// <c>while (true) { if (!cond) break; work }</c> both hoist to <c>while (cond) { work }</c>. Only the
/// condition-hoist shape is reported; an empty guarded body or an else-only guard is left to other rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2263HoistLoopConditionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.HoistLoopCondition);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.WhileStatement);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ForStatement);
    }

    /// <summary>Matches a hoistable infinite loop and returns the header condition and the resulting body.</summary>
    /// <param name="loop">The loop statement.</param>
    /// <param name="condition">The condition to move into the <c>while</c> header.</param>
    /// <param name="body">The body the hoisted loop should carry.</param>
    /// <returns><see langword="true"/> when the loop is a condition-hoist shape.</returns>
    internal static bool TryGetHoist(StatementSyntax loop, out ExpressionSyntax condition, out StatementSyntax body)
    {
        condition = null!;
        body = null!;
        return IsInfiniteLoop(loop, out var block)
            && (TryMatchIfElseBreak(block, out condition, out body) || TryMatchGuardThenBreak(block, out condition, out body));
    }

    /// <summary>Reports a hoistable infinite loop on its leading keyword.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var loop = (StatementSyntax)context.Node;
        if (!TryGetHoist(loop, out _, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.HoistLoopCondition, loop.GetFirstToken().GetLocation()));
    }

    /// <summary>Matches the <c>if (cond) { work } else break;</c> shape, where the else only breaks.</summary>
    /// <param name="block">The loop body block.</param>
    /// <param name="condition">The condition to move into the header.</param>
    /// <param name="body">The non-empty then-branch that becomes the loop body.</param>
    /// <returns><see langword="true"/> when the block matches.</returns>
    private static bool TryMatchIfElseBreak(BlockSyntax block, out ExpressionSyntax condition, out StatementSyntax body)
    {
        condition = null!;
        body = null!;
        if (block.Statements.Count != 1
            || block.Statements[0] is not IfStatementSyntax { Else: { } elseClause } ifElse
            || !IsBreakOnly(elseClause.Statement)
            || IsEmpty(ifElse.Statement))
        {
            return false;
        }

        condition = ifElse.Condition;
        body = ifElse.Statement;
        return true;
    }

    /// <summary>Matches the leading <c>if (!cond) break;</c> guard followed by the work.</summary>
    /// <param name="block">The loop body block.</param>
    /// <param name="condition">The negated guard, which becomes the header condition.</param>
    /// <param name="body">The block with the guard removed.</param>
    /// <returns><see langword="true"/> when the block matches.</returns>
    private static bool TryMatchGuardThenBreak(BlockSyntax block, out ExpressionSyntax condition, out StatementSyntax body)
    {
        condition = null!;
        body = null!;
        if (block.Statements.Count < 2
            || block.Statements[0] is not IfStatementSyntax { Else: null } guard
            || !IsBreakOnly(guard.Statement))
        {
            return false;
        }

        condition = Negate(guard.Condition);
        body = block.WithStatements(block.Statements.RemoveAt(0));
        return true;
    }

    /// <summary>Returns whether a loop is an infinite loop with a block body.</summary>
    /// <param name="loop">The loop statement.</param>
    /// <param name="block">The loop's block body.</param>
    /// <returns><see langword="true"/> for <c>while (true) { … }</c> and <c>for (;;) { … }</c>.</returns>
    private static bool IsInfiniteLoop(StatementSyntax loop, out BlockSyntax block)
    {
        switch (loop)
        {
            case WhileStatementSyntax { Statement: BlockSyntax whileBody } whileLoop
                when whileLoop.Condition.IsKind(SyntaxKind.TrueLiteralExpression):
            {
                block = whileBody;
                return true;
            }

            case ForStatementSyntax { Condition: null, Declaration: null, Statement: BlockSyntax forBody } forLoop
                when forLoop.Initializers.Count == 0 && forLoop.Incrementors.Count == 0:
            {
                block = forBody;
                return true;
            }

            default:
            {
                block = null!;
                return false;
            }
        }
    }

    /// <summary>Returns whether a statement is exactly a <c>break;</c> (bare or in a single-statement block).</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <returns><see langword="true"/> when the statement only breaks.</returns>
    private static bool IsBreakOnly(StatementSyntax statement)
        => statement is BreakStatementSyntax
            || (statement is BlockSyntax { Statements.Count: 1 } block && block.Statements[0] is BreakStatementSyntax);

    /// <summary>Returns whether a statement is an empty block or an empty statement.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <returns><see langword="true"/> when the statement carries no work.</returns>
    private static bool IsEmpty(StatementSyntax statement)
        => statement is EmptyStatementSyntax or BlockSyntax { Statements.Count: 0 };

    /// <summary>Negates a guard condition so it can head the loop.</summary>
    /// <param name="condition">The guard condition.</param>
    /// <returns>The negated condition, unwrapping a leading <c>!</c> where present.</returns>
    private static ExpressionSyntax Negate(ExpressionSyntax condition)
    {
        var inner = Unparenthesize(condition);
        if (inner is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation)
        {
            return Unparenthesize(negation.Operand).WithoutTrivia();
        }

        var operand = PrimaryExpressionClassification.IsPrimary(inner)
            ? inner.WithoutTrivia()
            : SyntaxFactory.ParenthesizedExpression(inner.WithoutTrivia());
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand);
    }

    /// <summary>Strips redundant parentheses from a condition.</summary>
    /// <param name="expression">The condition.</param>
    /// <returns>The condition with any surrounding parentheses removed.</returns>
    private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
