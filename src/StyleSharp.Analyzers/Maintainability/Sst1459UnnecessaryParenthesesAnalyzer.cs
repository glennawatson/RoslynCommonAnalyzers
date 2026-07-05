// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports only grouping parentheses that wrap a standalone expression in a containing syntax
/// node that already separates it from neighboring operators. This intentionally avoids the broad
/// expression-precedence problem and stays syntax-only: no semantic model, no speculative binds,
/// and no allocation-heavy expression simplification.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1459UnnecessaryParenthesesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RemoveUnnecessaryParentheses);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ParenthesizedExpression);
    }

    /// <summary>Reports standalone parenthesized expressions whose parentheses are not grouping operators.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var parenthesized = (ParenthesizedExpressionSyntax)context.Node;
        if (!IsSimpleOperand(parenthesized.Expression) || !IsIsolatedByParent(parenthesized))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.RemoveUnnecessaryParentheses, parenthesized.GetLocation()));
    }

    /// <summary>Returns whether the inner expression has no operator-precedence ambiguity.</summary>
    /// <param name="expression">The inner expression.</param>
    /// <returns><see langword="true"/> for simple operands.</returns>
    private static bool IsSimpleOperand(ExpressionSyntax expression)
        => IsNameOrAccess(expression.Kind()) || IsCreationOrLiteral(expression.Kind());

    /// <summary>Returns whether a syntax kind is a simple name, receiver, access, or call.</summary>
    /// <param name="kind">The expression kind.</param>
    /// <returns><see langword="true"/> for name-like operands.</returns>
    private static bool IsNameOrAccess(SyntaxKind kind)
        => kind is
            SyntaxKind.IdentifierName or
            SyntaxKind.GenericName or
            SyntaxKind.SimpleMemberAccessExpression or
            SyntaxKind.ElementAccessExpression or
            SyntaxKind.InvocationExpression or
            SyntaxKind.ThisExpression or
            SyntaxKind.BaseExpression;

    /// <summary>Returns whether a syntax kind is a creation or literal operand.</summary>
    /// <param name="kind">The expression kind.</param>
    /// <returns><see langword="true"/> for creation and literal operands.</returns>
    private static bool IsCreationOrLiteral(SyntaxKind kind)
        => kind is
            SyntaxKind.ObjectCreationExpression or
            SyntaxKind.ImplicitObjectCreationExpression or
            SyntaxKind.DefaultLiteralExpression or
            SyntaxKind.NumericLiteralExpression or
            SyntaxKind.StringLiteralExpression or
            SyntaxKind.CharacterLiteralExpression or
            SyntaxKind.TrueLiteralExpression or
            SyntaxKind.FalseLiteralExpression or
            SyntaxKind.NullLiteralExpression;

    /// <summary>Returns whether the parent syntax already isolates this expression.</summary>
    /// <param name="parenthesized">The parenthesized expression.</param>
    /// <returns><see langword="true"/> when removing parentheses cannot affect grouping.</returns>
    private static bool IsIsolatedByParent(ParenthesizedExpressionSyntax parenthesized)
        => parenthesized.Parent switch
        {
            ReturnStatementSyntax { Expression: var expression } => expression == parenthesized,
            ThrowStatementSyntax { Expression: var expression } => expression == parenthesized,
            ArrowExpressionClauseSyntax { Expression: var expression } => expression == parenthesized,
            EqualsValueClauseSyntax { Value: var value } => value == parenthesized,
            ArgumentSyntax { Expression: var expression } => expression == parenthesized,
            AssignmentExpressionSyntax { Right: var right } => right == parenthesized,
            _ => false
        };
}
