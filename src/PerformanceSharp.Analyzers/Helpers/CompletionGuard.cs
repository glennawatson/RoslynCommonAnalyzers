// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Decides whether a blocking wait sits somewhere control can only reach once the task is already
/// finished — the guarded fast path, which blocks nothing and is the whole point of
/// <c>IsCompletedSuccessfully</c>. Reporting that code would be telling the author to make correct
/// code worse, so the recognized shapes are deliberately generous and the analysis errs toward
/// staying silent: an unrecognized reassignment between the check and the wait costs a report we
/// would otherwise have made, which is the cheap direction to be wrong in.
/// <para>
/// Recognized: the task is checked with <c>IsCompleted</c> or <c>IsCompletedSuccessfully</c>, and
/// the wait sits in the <c>if</c> body that check gates, in the <c>else</c> of the negated check,
/// in either branch of a ternary on it, to the right of an <c>&amp;&amp;</c> whose left proves it
/// (or an <c>||</c> whose left disproves it), after an early-return guard that has already left
/// the method when the task was not complete, or after the task was awaited outright. Conditions
/// are walked through parentheses, <c>!</c>, <c>&amp;&amp;</c> and <c>||</c>.
/// </para>
/// <para>
/// Not recognized, and therefore still reported: a check routed through a local
/// (<c>var done = t.IsCompleted;</c>), a pattern (<c>t.IsCompleted is true</c>), a
/// <c>Status == TaskStatus.RanToCompletion</c> comparison, a check on a task the wait does not
/// name identically, and a check on anything but a stable expression — a task fetched by calling
/// something (<c>Load().IsCompleted</c>) is a different task each time it is fetched, so it can
/// never guard anything.
/// </para>
/// </summary>
internal static class CompletionGuard
{
    /// <summary>The completion check every task shape has.</summary>
    private const string IsCompletedPropertyName = "IsCompleted";

    /// <summary>The completion check that also excludes faulted and canceled tasks.</summary>
    private const string IsCompletedSuccessfullyPropertyName = "IsCompletedSuccessfully";

    /// <summary>Returns whether a completion check already proved the task cannot block here.</summary>
    /// <param name="blocking">The blocking expression.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when control only reaches the wait with the task complete.</returns>
    public static bool IsProvablyComplete(SyntaxNode blocking, ExpressionSyntax target)
    {
        if (!IsStable(target))
        {
            return false;
        }

        for (var child = blocking; child.Parent is { } parent; child = parent)
        {
            if (IsFunctionBoundary(parent))
            {
                return false;
            }

            if (ParentProves(parent, child, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node ends the search — a guard outside a function body cannot speak for code inside it.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> when the walk must stop.</returns>
    /// <remarks>
    /// A lambda or a local function can run long after the check that encloses it, by which time
    /// the task it checked may have been replaced; only guards inside the same function body count.
    /// </remarks>
    private static bool IsFunctionBoundary(SyntaxNode node)
        => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax;

    /// <summary>Returns whether one step up the tree lands inside something a completion check gates.</summary>
    /// <param name="parent">The parent node.</param>
    /// <param name="child">The child the walk came from.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when the parent proves the task is complete.</returns>
    private static bool ParentProves(SyntaxNode parent, SyntaxNode child, ExpressionSyntax target)
        => parent switch
        {
            IfStatementSyntax ifStatement => IfProves(ifStatement, child, target),
            ConditionalExpressionSyntax ternary => TernaryProves(ternary, child, target),
            BinaryExpressionSyntax binary => BinaryProves(binary, child, target),
            BlockSyntax block => PrecedingStatementsProve(block.Statements, child, target),
            _ => false,
        };

    /// <summary>Returns whether an <c>if</c> statement's condition proves the branch the wait sits in.</summary>
    /// <param name="ifStatement">The if statement.</param>
    /// <param name="child">The child the walk came from.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when the branch is only reached with the task complete.</returns>
    private static bool IfProves(IfStatementSyntax ifStatement, SyntaxNode child, ExpressionSyntax target)
    {
        if (child == ifStatement.Statement)
        {
            return ConditionProves(ifStatement.Condition, target, whenTrue: true);
        }

        return ifStatement.Else is { } elseClause
            && child == elseClause.Statement
            && ConditionProves(ifStatement.Condition, target, whenTrue: false);
    }

    /// <summary>Returns whether a ternary's condition proves the arm the wait sits in.</summary>
    /// <param name="ternary">The conditional expression.</param>
    /// <param name="child">The child the walk came from.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when the arm is only evaluated with the task complete.</returns>
    private static bool TernaryProves(ConditionalExpressionSyntax ternary, SyntaxNode child, ExpressionSyntax target)
    {
        if (child == ternary.WhenTrue)
        {
            return ConditionProves(ternary.Condition, target, whenTrue: true);
        }

        return child == ternary.WhenFalse && ConditionProves(ternary.Condition, target, whenTrue: false);
    }

    /// <summary>Returns whether a short-circuiting operator's left operand proves its right one.</summary>
    /// <param name="binary">The binary expression.</param>
    /// <param name="child">The child the walk came from.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when the right operand only evaluates with the task complete.</returns>
    /// <remarks>
    /// <c>t.IsCompleted &amp;&amp; t.Result</c> evaluates the wait only when the left side held;
    /// <c>!t.IsCompleted || t.Result</c> only when it did not.
    /// </remarks>
    private static bool BinaryProves(BinaryExpressionSyntax binary, SyntaxNode child, ExpressionSyntax target)
    {
        if (child != binary.Right)
        {
            return false;
        }

        if (binary.IsKind(SyntaxKind.LogicalAndExpression))
        {
            return ConditionProves(binary.Left, target, whenTrue: true);
        }

        return binary.IsKind(SyntaxKind.LogicalOrExpression) && ConditionProves(binary.Left, target, whenTrue: false);
    }

    /// <summary>Returns whether a statement earlier in the same block already settled the task.</summary>
    /// <param name="statements">The block's statements.</param>
    /// <param name="child">The statement the wait sits in.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when an earlier statement proves the task is complete.</returns>
    private static bool PrecedingStatementsProve(SyntaxList<StatementSyntax> statements, SyntaxNode child, ExpressionSyntax target)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            if (statement == child)
            {
                return false;
            }

            if (StatementProves(statement, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether one earlier statement leaves the task complete for everything after it.</summary>
    /// <param name="statement">The earlier statement.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when the statement is an early-return guard or an await of the task.</returns>
    private static bool StatementProves(StatementSyntax statement, ExpressionSyntax target)
        => statement switch
        {
            IfStatementSyntax { Else: null } guard =>
                Exits(guard.Statement) && ConditionProves(guard.Condition, target, whenTrue: false),
            ExpressionStatementSyntax { Expression: AwaitExpressionSyntax awaited } => AwaitsTarget(awaited, target),
            LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1 } local =>
                local.Declaration.Variables[0].Initializer is { Value: AwaitExpressionSyntax awaitedValue }
                && AwaitsTarget(awaitedValue, target),
            _ => false,
        };

    /// <summary>Returns whether an await expression awaits the very task the wait blocks on.</summary>
    /// <param name="awaited">The await expression.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when the awaited task is the same one.</returns>
    private static bool AwaitsTarget(AwaitExpressionSyntax awaited, ExpressionSyntax target)
        => SyntaxFactory.AreEquivalent(BlockingWait.UnwrapConfigureAwait(awaited.Expression), target);

    /// <summary>Returns whether a statement cannot complete normally, so falling past its guard means the guard did not hold.</summary>
    /// <param name="statement">The guard's body.</param>
    /// <returns><see langword="true"/> when control leaves rather than falls through.</returns>
    private static bool Exits(StatementSyntax statement)
        => statement switch
        {
            ReturnStatementSyntax or ThrowStatementSyntax or BreakStatementSyntax or ContinueStatementSyntax => true,
            BlockSyntax { Statements.Count: > 0 } block => Exits(block.Statements[block.Statements.Count - 1]),
            _ => false,
        };

    /// <summary>Returns whether a condition holding the given way proves the task is complete.</summary>
    /// <param name="condition">The condition to read.</param>
    /// <param name="target">The task blocked on.</param>
    /// <param name="whenTrue">Whether the condition is known true or known false.</param>
    /// <returns><see langword="true"/> when that knowledge implies completion.</returns>
    private static bool ConditionProves(ExpressionSyntax condition, ExpressionSyntax target, bool whenTrue)
        => condition switch
        {
            ParenthesizedExpressionSyntax parenthesized => ConditionProves(parenthesized.Expression, target, whenTrue),
            PrefixUnaryExpressionSyntax negation when negation.IsKind(SyntaxKind.LogicalNotExpression) =>
                ConditionProves(negation.Operand, target, !whenTrue),
            BinaryExpressionSyntax binary => OperandProves(binary, target, whenTrue),
            MemberAccessExpressionSyntax check => whenTrue && ChecksCompletion(check, target),
            _ => false,
        };

    /// <summary>Returns whether either operand of a short-circuiting condition carries the completion check.</summary>
    /// <param name="binary">The condition.</param>
    /// <param name="target">The task blocked on.</param>
    /// <param name="whenTrue">Whether the condition is known true or known false.</param>
    /// <returns><see langword="true"/> when the known truth of the whole implies the known truth of a checking operand.</returns>
    /// <remarks>
    /// A true <c>a &amp;&amp; b</c> makes both operands true, and a false <c>a || b</c> makes both
    /// false; nothing follows from a false conjunction or a true disjunction.
    /// </remarks>
    private static bool OperandProves(BinaryExpressionSyntax binary, ExpressionSyntax target, bool whenTrue)
    {
        var distributes = whenTrue
            ? binary.IsKind(SyntaxKind.LogicalAndExpression)
            : binary.IsKind(SyntaxKind.LogicalOrExpression);

        return distributes
            && (ConditionProves(binary.Left, target, whenTrue) || ConditionProves(binary.Right, target, whenTrue));
    }

    /// <summary>Returns whether a member access is a completion check on the task blocked on.</summary>
    /// <param name="check">The member access.</param>
    /// <param name="target">The task blocked on.</param>
    /// <returns><see langword="true"/> when it reads that task's completion.</returns>
    private static bool ChecksCompletion(MemberAccessExpressionSyntax check, ExpressionSyntax target)
        => check.Name.Identifier.ValueText is IsCompletedPropertyName or IsCompletedSuccessfullyPropertyName
            && SyntaxFactory.AreEquivalent(check.Expression, target);

    /// <summary>Returns whether an expression names the same task every time it is written.</summary>
    /// <param name="expression">The task expression.</param>
    /// <returns><see langword="true"/> for a local, parameter, or field path; <see langword="false"/> once a call is involved.</returns>
    private static bool IsStable(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax or ThisExpressionSyntax => true,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax } access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) =>
                IsStable(access.Expression),
            _ => false,
        };
}
