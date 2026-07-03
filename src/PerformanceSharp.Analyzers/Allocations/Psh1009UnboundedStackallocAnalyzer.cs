// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>stackalloc</c> expressions whose length is neither a compile-time constant nor
/// visibly bounded by one (PSH1009). Recognized bounds keep the rule false-positive-averse: a
/// constant or const/static-readonly-field length, a <c>Math.Min</c>/<c>Math.Clamp</c> length, a
/// surrounding conditional expression whose condition compares against a constant, or an
/// enclosing <c>if</c> whose condition does. Anything that looks bounded is trusted; only a raw
/// data-driven length with no constant in sight is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1009UnboundedStackallocAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.UnboundedStackalloc);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeStackalloc, SyntaxKind.StackAllocArrayCreationExpression);
    }

    /// <summary>Reports PSH1009 for a stackalloc whose length is data-driven with no constant bound in sight.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStackalloc(SyntaxNodeAnalysisContext context)
    {
        var stackallocExpression = (StackAllocArrayCreationExpressionSyntax)context.Node;
        if (stackallocExpression.Type is not ArrayTypeSyntax arrayType
            || arrayType.RankSpecifiers.Count == 0
            || arrayType.RankSpecifiers[0].Sizes.Count != 1
            || arrayType.RankSpecifiers[0].Sizes[0] is OmittedArraySizeExpressionSyntax)
        {
            return;
        }

        var size = arrayType.RankSpecifiers[0].Sizes[0];
        if (IsBoundedSize(context, size) || IsGuarded(context, stackallocExpression))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.UnboundedStackalloc,
            stackallocExpression.SyntaxTree,
            stackallocExpression.Span));
    }

    /// <summary>Returns whether the length expression itself carries a bound.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="size">The length expression.</param>
    /// <returns><see langword="true"/> for constants, const or static readonly fields, and Min/Clamp results.</returns>
    private static bool IsBoundedSize(SyntaxNodeAnalysisContext context, ExpressionSyntax size)
    {
        if (context.SemanticModel.GetConstantValue(size, context.CancellationToken).HasValue)
        {
            return true;
        }

        if (size is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax sizeCall }
            && sizeCall.Name.Identifier.ValueText is "Min" or "Clamp")
        {
            return true;
        }

        return context.SemanticModel.GetSymbolInfo(size, context.CancellationToken).Symbol
            is IFieldSymbol { IsStatic: true, IsReadOnly: true };
    }

    /// <summary>Returns whether a surrounding conditional or if statement compares against a constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="stackallocExpression">The stackalloc expression.</param>
    /// <returns><see langword="true"/> when a constant guard encloses the stackalloc.</returns>
    private static bool IsGuarded(SyntaxNodeAnalysisContext context, StackAllocArrayCreationExpressionSyntax stackallocExpression)
    {
        for (SyntaxNode? current = stackallocExpression; current is not null; current = current.Parent)
        {
            switch (current.Parent)
            {
                case ConditionalExpressionSyntax conditional when conditional.Condition != current
                    && ConditionComparesConstant(context, conditional.Condition):
                    return true;
                case IfStatementSyntax ifStatement when ifStatement.Condition != current
                    && ConditionComparesConstant(context, ifStatement.Condition):
                    return true;
                case MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax:
                    return false;
                default:
                    continue;
            }
        }

        return false;
    }

    /// <summary>Returns whether a condition contains a comparison against a compile-time constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="condition">The condition to inspect.</param>
    /// <returns><see langword="true"/> when a constant relational comparison or relational pattern is found.</returns>
    private static bool ConditionComparesConstant(SyntaxNodeAnalysisContext context, ExpressionSyntax condition)
        => condition switch
        {
            BinaryExpressionSyntax binary when binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression
                => ConditionComparesConstant(context, binary.Left) || ConditionComparesConstant(context, binary.Right),
            BinaryExpressionSyntax binary when IsComparisonKind(binary.Kind())
                => context.SemanticModel.GetConstantValue(binary.Left, context.CancellationToken).HasValue
                    || context.SemanticModel.GetConstantValue(binary.Right, context.CancellationToken).HasValue,
            IsPatternExpressionSyntax pattern => PatternHasRelationalConstant(pattern.Pattern),
            ParenthesizedExpressionSyntax parenthesized => ConditionComparesConstant(context, parenthesized.Expression),
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation
                => ConditionComparesConstant(context, negation.Operand),
            _ => false,
        };

    /// <summary>Returns whether a syntax kind is a relational or equality comparison.</summary>
    /// <param name="kind">The syntax kind to classify.</param>
    /// <returns><see langword="true"/> for the comparison operators a guard can use.</returns>
    private static bool IsComparisonKind(SyntaxKind kind)
        => kind is SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.EqualsExpression;

    /// <summary>Returns whether a pattern contains a relational or constant sub-pattern.</summary>
    /// <param name="pattern">The pattern to inspect.</param>
    /// <returns><see langword="true"/> when a bounding pattern is found.</returns>
    private static bool PatternHasRelationalConstant(PatternSyntax pattern)
        => pattern switch
        {
            RelationalPatternSyntax => true,
            ConstantPatternSyntax => true,
            BinaryPatternSyntax binary => PatternHasRelationalConstant(binary.Left) || PatternHasRelationalConstant(binary.Right),
            UnaryPatternSyntax unary => PatternHasRelationalConstant(unary.Pattern),
            ParenthesizedPatternSyntax parenthesized => PatternHasRelationalConstant(parenthesized.Pattern),
            _ => false,
        };
}
