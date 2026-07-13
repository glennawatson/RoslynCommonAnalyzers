// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a guard that tests a value against what it then assigns (SST1492): <c>if (x != y) { x = y; }</c>,
/// and the inverted <c>if (x == y) { } else { x = y; }</c>. The guard only skips an assignment that would
/// have changed nothing, so the whole statement collapses to the assignment.
/// </summary>
/// <remarks>
/// <para>
/// The shape is only pointless while the assignment is pure storage. A property whose setter is written by
/// hand can do anything behind the assignment — raise a change notification, validate, dirty a flag — and the
/// guard is then load-bearing, so a target that is a property is reported only when its setter is
/// auto-implemented. A property from another assembly is left alone: nothing in this compilation can prove
/// what its setter does.
/// </para>
/// <para>
/// Both operands must be side-effect free, or the guard is what stops the second evaluation from happening.
/// A compound assignment reads before it writes, so it is never the same operation as the test and is never
/// reported.
/// </para>
/// <para>
/// The clean path is three syntax tests — a binary condition of the right kind, a single-statement branch, a
/// simple assignment — and only a statement that has already matched the whole shape is bound.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1492SelfAssignmentGuardAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.SelfAssignmentGuard);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
    }

    /// <summary>Finds the assignment a guard is wrapped around, when the guard tests that very assignment.</summary>
    /// <param name="ifStatement">The if statement to match.</param>
    /// <returns>The guarded assignment statement, or <see langword="null"/> when the shape does not match.</returns>
    /// <remarks>Shared with the code fix so the fix re-proves the shape against the tree it is about to edit.</remarks>
    internal static ExpressionStatementSyntax? TryGetGuardedAssignment(IfStatementSyntax ifStatement)
    {
        if (ifStatement.Condition is not BinaryExpressionSyntax condition)
        {
            return null;
        }

        var guarded = condition.Kind() switch
        {
            SyntaxKind.NotEqualsExpression when ifStatement.Else is null => ifStatement.Statement,
            SyntaxKind.EqualsExpression when ifStatement.Else is { } elseClause && IsEmpty(ifStatement.Statement) => elseClause.Statement,
            _ => null,
        };

        if (GetSingleStatement(guarded) is not ExpressionStatementSyntax statement
            || statement.Expression is not AssignmentExpressionSyntax assignment
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return null;
        }

        return IsTheTestedPair(condition, assignment.Left, assignment.Right) ? statement : null;
    }

    /// <summary>Reports one guard whose condition tests the assignment it guards.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (TryGetGuardedAssignment(ifStatement) is not { Expression: AssignmentExpressionSyntax assignment })
        {
            return;
        }

        if (!HasSideEffectFreeSetter(assignment.Left, context))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.SelfAssignmentGuard,
            ifStatement.Condition.GetLocation(),
            assignment.Left.ToString()));
    }

    /// <summary>Returns whether the assignment's two sides are the two sides of the condition.</summary>
    /// <param name="condition">The guard's condition.</param>
    /// <param name="target">The assignment's target.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns><see langword="true"/> when the guard tests exactly what the assignment does.</returns>
    /// <remarks>
    /// The pair is unordered — <c>if (y != x) { x = y; }</c> is the same mistake written the other way round —
    /// and both sides must be side-effect free, or skipping the assignment also skips an evaluation.
    /// </remarks>
    private static bool IsTheTestedPair(BinaryExpressionSyntax condition, ExpressionSyntax target, ExpressionSyntax value)
    {
        if (!SideEffectFreeExpression.IsSideEffectFree(target) || !SideEffectFreeExpression.IsSideEffectFree(value))
        {
            return false;
        }

        return (SyntaxFactory.AreEquivalent(condition.Left, target, topLevel: false)
                && SyntaxFactory.AreEquivalent(condition.Right, value, topLevel: false))
            || (SyntaxFactory.AreEquivalent(condition.Left, value, topLevel: false)
                && SyntaxFactory.AreEquivalent(condition.Right, target, topLevel: false));
    }

    /// <summary>Returns whether assigning the target can do nothing but store the value.</summary>
    /// <param name="target">The assignment's target.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when the assignment has no behavior the guard could be protecting.</returns>
    /// <remarks>
    /// A field, a local or a parameter stores and nothing else. A property is only safe when its setter is
    /// auto-implemented: a hand-written setter — the change-notification shape — is exactly the case the guard
    /// exists for. A property this compilation has no source for is treated as hand-written.
    /// </remarks>
    private static bool HasSideEffectFreeSetter(ExpressionSyntax target, SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol is not IPropertySymbol property)
        {
            return true;
        }

        if (property.SetMethod is not { } setter)
        {
            return false;
        }

        var declarations = setter.DeclaringSyntaxReferences;
        if (declarations.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < declarations.Length; i++)
        {
            if (declarations[i].GetSyntax(context.CancellationToken) is not AccessorDeclarationSyntax accessor
                || accessor.Body is not null
                || accessor.ExpressionBody is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the one statement a branch runs, unwrapping a block that holds exactly one.</summary>
    /// <param name="statement">The branch's statement.</param>
    /// <returns>The single statement, or <see langword="null"/> when the branch does more than one thing.</returns>
    private static StatementSyntax? GetSingleStatement(StatementSyntax? statement) => statement switch
    {
        BlockSyntax { Statements.Count: 1 } block => block.Statements[0],
        BlockSyntax => null,
        _ => statement,
    };

    /// <summary>Returns whether a branch does nothing at all.</summary>
    /// <param name="statement">The branch's statement.</param>
    /// <returns><see langword="true"/> for an empty block, which is what the inverted shape leaves behind.</returns>
    private static bool IsEmpty(StatementSyntax statement) => statement is BlockSyntax { Statements.Count: 0 };
}
