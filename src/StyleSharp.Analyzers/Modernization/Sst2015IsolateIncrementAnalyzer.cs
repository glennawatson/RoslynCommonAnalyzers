// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>++</c> or <c>--</c> buried inside a larger expression (SST2015) — <c>array[i++] = Compute(i)</c>,
/// <c>Foo(x++ + y)</c> — where the result depends on an evaluation order readers reliably guess wrong.
/// </summary>
/// <remarks>
/// <para>
/// An increment that <em>is</em> the expression is not buried, and is not reported: the statement <c>i++;</c>,
/// a <c>for</c> loop's initializer or incrementor, the whole value of a store (<c>var next = i++;</c>,
/// <c>_index = i++;</c>), the whole returned value (<c>return i++;</c>, <c>=> _next++;</c>), and a lambda whose
/// body is the increment. In every one of those the increment happens once, in a place with nothing to be
/// ordered against.
/// </para>
/// <para>
/// There is deliberately no code fix. Hoisting the increment into its own statement changes when it runs
/// relative to the rest of the expression — which is the whole point of the diagnostic. Only the author knows
/// which order was intended, and a fix that silently picks one would be changing behavior while claiming to
/// clarify it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2015IsolateIncrementAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.IsolateIncrement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.PreIncrementExpression,
            SyntaxKind.PreDecrementExpression,
            SyntaxKind.PostIncrementExpression,
            SyntaxKind.PostDecrementExpression);
    }

    /// <summary>Returns whether an increment's value is consumed by a surrounding expression.</summary>
    /// <param name="node">The increment or decrement expression.</param>
    /// <returns><see langword="true"/> when the increment is buried rather than standing alone.</returns>
    internal static bool IsBuried(ExpressionSyntax node)
    {
        var current = (SyntaxNode)node;
        var parent = current.Parent;
        while (parent is ParenthesizedExpressionSyntax)
        {
            current = parent;
            parent = parent.Parent;
        }

        return parent is not null && !IsStandalone(parent, current);
    }

    /// <summary>Returns whether the position an increment sits in makes it the whole expression, not part of one.</summary>
    /// <param name="parent">The increment's parent, past any parentheses.</param>
    /// <param name="value">The increment, or the parentheses around it.</param>
    /// <returns><see langword="true"/> when the increment happens once, with nothing to be ordered against.</returns>
    private static bool IsStandalone(SyntaxNode parent, SyntaxNode value) => parent switch
    {
        ExpressionStatementSyntax => true,
        ForStatementSyntax => true,
        ReturnStatementSyntax => true,
        ArrowExpressionClauseSyntax => true,
        LambdaExpressionSyntax lambda => lambda.Body == value,
        EqualsValueClauseSyntax equals => equals.Value == value,
        AssignmentExpressionSyntax assignment => IsWholeValueOfStatementAssignment(assignment, value),
        _ => false,
    };

    /// <summary>Reports one increment that a larger expression reads.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = (ExpressionSyntax)context.Node;
        if (!IsBuried(node))
        {
            return;
        }

        var operand = GetOperand(node);
        if (operand is null)
        {
            return;
        }

        var diagnostic = DiagnosticHelper.Create(
            ModernizationRules.IsolateIncrement,
            node.GetLocation(),
            node.ToString(),
            operand.ToString());
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>Returns whether an assignment is a plain store whose whole value is the increment.</summary>
    /// <param name="assignment">The assignment that reads the increment.</param>
    /// <param name="value">The increment, or the parentheses around it.</param>
    /// <returns><see langword="true"/> when the increment is the entire right-hand side of a statement's store.</returns>
    /// <remarks>
    /// A compound assignment (<c>total += i++</c>) reads its target as well as writing it, so the order is
    /// back in question and the increment is reported.
    /// </remarks>
    private static bool IsWholeValueOfStatementAssignment(AssignmentExpressionSyntax assignment, SyntaxNode value)
        => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && assignment.Right == value
            && assignment.Parent is ExpressionStatementSyntax;

    /// <summary>Gets the variable an increment reads and writes.</summary>
    /// <param name="node">The increment or decrement expression.</param>
    /// <returns>The operand, or <see langword="null"/> when the expression is not an increment.</returns>
    private static ExpressionSyntax? GetOperand(ExpressionSyntax node) => node switch
    {
        PrefixUnaryExpressionSyntax prefix => prefix.Operand,
        PostfixUnaryExpressionSyntax postfix => postfix.Operand,
        _ => null,
    };
}
