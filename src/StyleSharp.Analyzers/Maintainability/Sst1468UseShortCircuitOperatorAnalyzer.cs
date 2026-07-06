// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the non-short-circuiting <c>&amp;</c> and <c>|</c> operators applied to two boolean
/// operands (SST1468). The eager forms always evaluate both sides, while <c>&amp;&amp;</c> and
/// <c>||</c> compute the same value and stop as soon as the left operand decides the answer. The
/// hot path stays syntactic: the right operand must match a recursive side-effect-free whitelist
/// (identifiers, member-access chains, literals, logical negation, parenthesized content, and
/// simple comparisons of those) before the semantic model confirms both operands are boolean.
/// Candidates inside lambdas converted to expression trees are skipped because the rewrite would
/// change the produced tree shape.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1468UseShortCircuitOperatorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The replacement operator reported for a boolean <c>&amp;</c>.</summary>
    private const string LogicalAndText = "&&";

    /// <summary>The replacement operator reported for a boolean <c>|</c>.</summary>
    private const string LogicalOrText = "||";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UseShortCircuitOperator);

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

    /// <summary>Reports a boolean <c>&amp;</c> / <c>|</c> whose right operand is safe to skip.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expressionType">The resolved <c>System.Linq.Expressions.Expression&lt;TDelegate&gt;</c> definition, if any.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!IsSideEffectFree(binary.Right))
        {
            return;
        }

        if (!IsBoolean(binary.Left, context.SemanticModel, context.CancellationToken)
            || !IsBoolean(binary.Right, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (expressionType is not null && IsInExpressionTree(binary, context.SemanticModel, expressionType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.UseShortCircuitOperator,
            binary.SyntaxTree,
            binary.OperatorToken.Span,
            binary.IsKind(SyntaxKind.BitwiseAndExpression) ? LogicalAndText : LogicalOrText));
    }

    /// <summary>Returns whether skipping the expression's evaluation cannot change behavior.</summary>
    /// <param name="expression">The candidate right operand.</param>
    /// <returns><see langword="true"/> for plain reads, literals, negations, parenthesized content, and simple comparisons of those.</returns>
    private static bool IsSideEffectFree(ExpressionSyntax expression)
    {
        var kind = expression.Kind();
        if (IsPlainReadOrLiteralKind(kind))
        {
            return true;
        }

        return kind switch
        {
            SyntaxKind.SimpleMemberAccessExpression => IsSideEffectFree(((MemberAccessExpressionSyntax)expression).Expression),
            SyntaxKind.ParenthesizedExpression => IsSideEffectFree(((ParenthesizedExpressionSyntax)expression).Expression),
            SyntaxKind.LogicalNotExpression => IsSideEffectFree(((PrefixUnaryExpressionSyntax)expression).Operand),
            _ => IsComparisonKind(kind) && IsSideEffectFreeComparison(expression),
        };
    }

    /// <summary>Returns whether a syntax kind is a plain read or literal that can never have side effects.</summary>
    /// <param name="kind">The expression's syntax kind.</param>
    /// <returns><see langword="true"/> for identifiers, <c>this</c>/<c>base</c>, and simple literals.</returns>
    private static bool IsPlainReadOrLiteralKind(SyntaxKind kind)
        => kind is SyntaxKind.IdentifierName
            or SyntaxKind.ThisExpression
            or SyntaxKind.BaseExpression
            or SyntaxKind.TrueLiteralExpression
            or SyntaxKind.FalseLiteralExpression
            or SyntaxKind.NumericLiteralExpression
            or SyntaxKind.StringLiteralExpression
            or SyntaxKind.CharacterLiteralExpression;

    /// <summary>Returns whether a syntax kind is one of the allowed comparison operators.</summary>
    /// <param name="kind">The expression's syntax kind.</param>
    /// <returns><see langword="true"/> for equality and relational operator expressions.</returns>
    private static bool IsComparisonKind(SyntaxKind kind)
        => kind is SyntaxKind.EqualsExpression
            or SyntaxKind.NotEqualsExpression
            or SyntaxKind.LessThanExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanOrEqualExpression;

    /// <summary>Returns whether a comparison expression compares two side-effect-free operands.</summary>
    /// <param name="expression">The comparison expression.</param>
    /// <returns><see langword="true"/> when both operands match the whitelist.</returns>
    private static bool IsSideEffectFreeComparison(ExpressionSyntax expression)
        => expression is BinaryExpressionSyntax comparison && IsSideEffectFree(comparison.Left) && IsSideEffectFree(comparison.Right);

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
