// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports string accumulation by concatenation inside a loop (PSH1206). A
/// <c>target += expr</c> or <c>target = target + ...</c> assignment whose string target
/// is declared outside the enclosing <c>for</c>/<c>foreach</c>/<c>while</c>/<c>do</c>
/// statement copies the whole accumulated value on every iteration, making the loop
/// quadratic. The ancestor walk stops at lambda, anonymous-method, local-function, and
/// member boundaries, so a lambda inside a loop is its own scope — but a loop inside the
/// lambda still counts. There is no code fix: introducing a <c>StringBuilder</c> is a
/// multi-statement redesign.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1206StringConcatenationInLoopAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.StringConcatenationInLoop);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.AddAssignmentExpression, SyntaxKind.SimpleAssignmentExpression);
    }

    /// <summary>Reports PSH1206 for a string concatenation assignment inside a loop.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var target = assignment.Left;
        if (!IsSimpleTarget(target)
            || (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && !IsSelfConcatenation(target, assignment.Right))
            || !TryGetEnclosingLoop(assignment, out var loop))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(target, context.CancellationToken).Type is not { SpecialType: SpecialType.System_String }
            || !IsDeclaredOutsideLoop(context.SemanticModel, target, loop!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.StringConcatenationInLoop,
            assignment.SyntaxTree,
            assignment.OperatorToken.Span,
            target.ToString()));
    }

    /// <summary>Returns whether the assignment target is an identifier or a simple member access.</summary>
    /// <param name="target">The assignment target.</param>
    /// <returns><see langword="true"/> for a target shape the rule tracks.</returns>
    private static bool IsSimpleTarget(ExpressionSyntax target)
        => target is IdentifierNameSyntax
            || (target is MemberAccessExpressionSyntax member && member.IsKind(SyntaxKind.SimpleMemberAccessExpression));

    /// <summary>
    /// Returns whether a simple assignment's right side is an additive chain whose leftmost
    /// operand is syntactically the target itself (<c>target = target + ...</c>).
    /// </summary>
    /// <param name="target">The assignment target.</param>
    /// <param name="right">The assignment's right side.</param>
    /// <returns><see langword="true"/> when the assignment re-concatenates the target.</returns>
    private static bool IsSelfConcatenation(ExpressionSyntax target, ExpressionSyntax right)
    {
        var leftmost = right;
        while (leftmost is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
        {
            leftmost = binary.Left;
        }

        return !ReferenceEquals(leftmost, right) && SyntaxFactory.AreEquivalent(target, leftmost, topLevel: false);
    }

    /// <summary>
    /// Walks up from the assignment looking for an enclosing loop statement, stopping at any
    /// lambda, anonymous-method, local-function, or member boundary so a loop outside the
    /// enclosing function body does not count.
    /// </summary>
    /// <param name="node">The assignment node to walk up from.</param>
    /// <param name="loop">The enclosing loop statement when found.</param>
    /// <returns><see langword="true"/> when a loop encloses the assignment within the same function.</returns>
    private static bool TryGetEnclosingLoop(SyntaxNode node, out StatementSyntax? loop)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                loop = (StatementSyntax)current;
                return true;
            }

            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax or AccessorDeclarationSyntax)
            {
                break;
            }
        }

        loop = null;
        return false;
    }

    /// <summary>
    /// Returns whether the target's symbol is declared outside the loop. Locals compare their
    /// declarator's position against the loop statement; fields, parameters, and properties
    /// always count as outside.
    /// </summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="target">The assignment target.</param>
    /// <param name="loop">The enclosing loop statement.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the accumulated value survives across iterations.</returns>
    private static bool IsDeclaredOutsideLoop(SemanticModel model, ExpressionSyntax target, StatementSyntax loop, CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(target, cancellationToken).Symbol;
        if (symbol is not ILocalSymbol local)
        {
            return symbol is IFieldSymbol or IParameterSymbol or IPropertySymbol;
        }

        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree == loop.SyntaxTree && loop.Span.Contains(reference.Span))
            {
                return false;
            }
        }

        return true;
    }
}
