// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports delegate null checks whose body immediately invokes the same delegate (SST2240).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2240ConditionalDelegateInvocationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseConditionalDelegateInvocation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
    }

    /// <summary>Reports a null-check guard whose only statement invokes the checked delegate.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (ifStatement.Else is not null
            || !TryGetNullCheckedExpression(ifStatement.Condition, out var checkedExpression)
            || !TryGetSingleInvocation(ifStatement.Statement, out var invocation)
            || !TryGetInvokedDelegateExpression(invocation, out var invokedExpression))
        {
            return;
        }

        var checkedSymbol = context.SemanticModel.GetSymbolInfo(checkedExpression, context.CancellationToken).Symbol;
        var invokedSymbol = context.SemanticModel.GetSymbolInfo(invokedExpression, context.CancellationToken).Symbol;
        if (checkedSymbol is null
            || !SymbolEqualityComparer.Default.Equals(checkedSymbol, invokedSymbol)
            || context.SemanticModel.GetTypeInfo(checkedExpression, context.CancellationToken).Type?.TypeKind != TypeKind.Delegate)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseConditionalDelegateInvocation, ifStatement.IfKeyword.GetLocation()));
    }

    /// <summary>Gets the expression that is compared to null.</summary>
    /// <param name="condition">The condition expression.</param>
    /// <param name="checkedExpression">The checked expression.</param>
    /// <returns><see langword="true"/> for supported not-null checks.</returns>
    private static bool TryGetNullCheckedExpression(ExpressionSyntax condition, out ExpressionSyntax checkedExpression)
    {
        condition = ExpressionSimplificationAnalyzer.Unwrap(condition);
        if (condition is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            var left = ExpressionSimplificationAnalyzer.Unwrap(binary.Left);
            var right = ExpressionSimplificationAnalyzer.Unwrap(binary.Right);
            if (right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                checkedExpression = left;
                return true;
            }

            if (left.IsKind(SyntaxKind.NullLiteralExpression))
            {
                checkedExpression = right;
                return true;
            }
        }

        if (condition is IsPatternExpressionSyntax pattern && IsNotNullPattern(pattern.Pattern))
        {
            checkedExpression = pattern.Expression;
            return true;
        }

        checkedExpression = null!;
        return false;
    }

    /// <summary>Returns whether a pattern is <c>not null</c>.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true"/> for the not-null pattern shape.</returns>
    private static bool IsNotNullPattern(PatternSyntax pattern)
        => pattern is UnaryPatternSyntax
        {
            OperatorToken.RawKind: (int)SyntaxKind.NotKeyword,
            Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression }
        };

    /// <summary>Gets the only invocation expression from a guarded statement.</summary>
    /// <param name="statement">The guarded statement.</param>
    /// <param name="invocation">The invocation.</param>
    /// <returns><see langword="true"/> when the body only invokes a delegate.</returns>
    private static bool TryGetSingleInvocation(StatementSyntax statement, out InvocationExpressionSyntax invocation)
    {
        if (statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax directInvocation })
        {
            invocation = directInvocation;
            return true;
        }

        if (statement is BlockSyntax { Statements.Count: 1 } block
            && block.Statements[0] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax blockInvocation })
        {
            invocation = blockInvocation;
            return true;
        }

        invocation = null!;
        return false;
    }

    /// <summary>Gets the delegate expression from a delegate invocation syntax shape.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="delegateExpression">The delegate expression.</param>
    /// <returns><see langword="true"/> when a delegate expression can be identified.</returns>
    private static bool TryGetInvokedDelegateExpression(InvocationExpressionSyntax invocation, out ExpressionSyntax delegateExpression)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Invoke" } invokeAccess)
        {
            delegateExpression = invokeAccess.Expression;
            return true;
        }

        delegateExpression = invocation.Expression;
        return true;
    }
}
