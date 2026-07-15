// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the non-short-circuiting <c>&amp;</c> and <c>|</c> operators applied to two boolean operands, in a
/// single walk. Both eager operators always evaluate their right operand, while <c>&amp;&amp;</c> and
/// <c>||</c> stop as soon as the left decides the answer.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1468 — the right operand is side-effect-free, so switching to the short-circuiting
/// operator is a pure tidy-up.</description></item>
/// <item><description>SST2415 — the right operand does real work (a call, an assignment, an increment, an
/// element access, an object creation), so the left operand reads like a guard that the eager operator
/// ignores; switching operators changes behaviour.</description></item>
/// </list>
/// <para>
/// The two ids are the two arms of one side-effect check the analyzer already computes. Candidates inside
/// lambdas converted to expression trees are skipped because the rewrite would change the produced tree
/// shape.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonShortCircuitOperatorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The replacement operator reported for a boolean <c>&amp;</c>.</summary>
    private const string LogicalAndText = "&&";

    /// <summary>The replacement operator reported for a boolean <c>|</c>.</summary>
    private const string LogicalOrText = "||";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.UseShortCircuitOperator,
        CorrectnessRules.NonShortCircuitGuard);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var expressionType = start.Compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, expressionType), SyntaxKind.BitwiseAndExpression, SyntaxKind.BitwiseOrExpression);
        });
    }

    /// <summary>Reports a boolean <c>&amp;</c> / <c>|</c>, choosing the id by whether the right operand does work.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expressionType">The resolved <c>System.Linq.Expressions.Expression&lt;TDelegate&gt;</c> definition, if any.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!IsBoolean(binary.Left, context.SemanticModel, context.CancellationToken)
            || !IsBoolean(binary.Right, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (expressionType is not null && IsInExpressionTree(binary, context.SemanticModel, expressionType, context.CancellationToken))
        {
            return;
        }

        var isAnd = binary.IsKind(SyntaxKind.BitwiseAndExpression);
        if (SideEffectFreeExpression.IsSideEffectFree(binary.Right))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.UseShortCircuitOperator,
                binary.SyntaxTree,
                binary.OperatorToken.Span,
                isAnd ? LogicalAndText : LogicalOrText));
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.NonShortCircuitGuard,
            binary.SyntaxTree,
            binary.OperatorToken.Span,
            binary.OperatorToken.Text));
    }

    /// <summary>Returns whether an operand's natural type is <see cref="SpecialType.System_Boolean"/>.</summary>
    /// <param name="operand">The operand to classify.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for boolean operands.</returns>
    private static bool IsBoolean(ExpressionSyntax operand, SemanticModel model, CancellationToken cancellationToken)
        => model.GetTypeInfo(operand, cancellationToken).Type is { SpecialType: SpecialType.System_Boolean };

    /// <summary>Returns whether the operator appears inside a lambda converted to an expression tree.</summary>
    /// <param name="node">The reported binary expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="expressionType">The resolved <c>Expression&lt;TDelegate&gt;</c> definition.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a containing lambda is an expression tree.</returns>
    private static bool IsInExpressionTree(SyntaxNode node, SemanticModel model, INamedTypeSymbol expressionType, CancellationToken cancellationToken)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax anonymous)
            {
                if (model.GetTypeInfo(anonymous, cancellationToken).ConvertedType is INamedTypeSymbol converted
                    && SymbolEqualityComparer.Default.Equals(converted.OriginalDefinition, expressionType))
                {
                    return true;
                }

                continue;
            }

            // Crossing a statement or member boundary leaves every enclosing expression, so no
            // ancestor lambda above this point can be the expression-tree boundary — stop before
            // walking to the root. Statement-bodied lambdas are never expression trees.
            if (current is StatementSyntax or MemberDeclarationSyntax)
            {
                break;
            }
        }

        return false;
    }
}
