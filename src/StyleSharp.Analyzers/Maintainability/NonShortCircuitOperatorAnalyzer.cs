// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
        MaintainabilityRules.UseShortCircuitOperator,
        CorrectnessRules.NonShortCircuitGuard);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var expressionType = new ExpressionTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, expressionType), SyntaxKind.BitwiseAndExpression, SyntaxKind.BitwiseOrExpression);
        });
    }

    /// <summary>Reports a boolean <c>&amp;</c> / <c>|</c>, choosing the id by whether the right operand does work.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expressionType">The expression-tree type, resolved on first demand.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, ExpressionTypes expressionType)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!IsBoolean(binary.Left, context.SemanticModel, context.CancellationToken)
            || !IsBoolean(binary.Right, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (IsInExpressionTree(binary, context.SemanticModel, expressionType, context.CancellationToken))
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
    private static bool IsBoolean(ExpressionSyntax operand, SemanticModel model, CancellationToken cancellationToken) =>
        model.GetTypeInfo(operand, cancellationToken).Type is { SpecialType: SpecialType.System_Boolean };

    /// <summary>Returns whether the operator appears inside a lambda converted to an expression tree.</summary>
    /// <param name="node">The reported binary expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="expressionType">The <c>Expression&lt;TDelegate&gt;</c> definition, resolved only inside a lambda.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a containing lambda is an expression tree.</returns>
    private static bool IsInExpressionTree(SyntaxNode node, SemanticModel model, ExpressionTypes expressionType, CancellationToken cancellationToken)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax anonymous)
            {
                if (expressionType.Get() is not { } resolved)
                {
                    return false;
                }

                if (model.GetTypeInfo(anonymous, cancellationToken).ConvertedType is INamedTypeSymbol converted
                    && SymbolEqualityComparer.Default.Equals(converted.OriginalDefinition, resolved))
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

    /// <summary>Resolves the expression-tree type only for a boolean operator inside a lambda.</summary>
    /// <param name="compilation">The compilation whose expression-tree type is resolved.</param>
    private sealed class ExpressionTypes(Compilation compilation)
    {
        /// <summary>The resolved type slot, including null when the type is absent.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the expression-tree type, caching its absence too.</summary>
        /// <returns>The expression-tree type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1")])[0];
    }
}
