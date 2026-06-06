// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires parentheses when arithmetic/shift operators of different precedence are
/// mixed (SST1407) and when <c>&amp;&amp;</c> and <c>||</c> are mixed (SST1408). The
/// inner sub-expression that should be parenthesized is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrecedenceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The precedence family of the multiplicative operators (* / %).</summary>
    private const int MultiplicativeFamily = 0;

    /// <summary>The precedence family of the additive operators (+ -).</summary>
    private const int AdditiveFamily = 1;

    /// <summary>The precedence family of the shift operators (&lt;&lt; &gt;&gt;).</summary>
    private const int ShiftFamily = 2;

    /// <summary>The sentinel for an operator that is not an arithmetic/shift operator.</summary>
    private const int NoFamily = -1;

    /// <summary>The operator kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.MultiplyExpression,
        SyntaxKind.DivideExpression,
        SyntaxKind.ModuloExpression,
        SyntaxKind.AddExpression,
        SyntaxKind.SubtractExpression,
        SyntaxKind.LeftShiftExpression,
        SyntaxKind.RightShiftExpression,
        SyntaxKind.LogicalAndExpression,
        SyntaxKind.LogicalOrExpression);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.ArithmeticPrecedence,
        MaintainabilityRules.ConditionalPrecedence);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports an inner binary expression whose precedence against its parent is implicit.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var inner = (BinaryExpressionSyntax)context.Node;
        if (inner.Parent is not BinaryExpressionSyntax parent)
        {
            return;
        }

        if (SelectRule(inner.Kind(), parent.Kind()) is not { } rule)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, inner.GetLocation()));
    }

    /// <summary>Returns the rule a parent/child operator pair violates, or <see langword="null"/> when none.</summary>
    /// <param name="inner">The inner operator kind.</param>
    /// <param name="parent">The parent operator kind.</param>
    /// <returns>The violated rule, or <see langword="null"/>.</returns>
    private static DiagnosticDescriptor? SelectRule(SyntaxKind inner, SyntaxKind parent)
    {
        if (IsConditional(inner) && IsConditional(parent))
        {
            return inner == parent ? null : MaintainabilityRules.ConditionalPrecedence;
        }

        var innerFamily = ArithmeticFamily(inner);
        var parentFamily = ArithmeticFamily(parent);
        if (innerFamily == NoFamily || parentFamily == NoFamily || innerFamily == parentFamily)
        {
            return null;
        }

        return MaintainabilityRules.ArithmeticPrecedence;
    }

    /// <summary>Returns whether the operator is a short-circuiting logical operator.</summary>
    /// <param name="kind">The operator kind.</param>
    /// <returns><see langword="true"/> for <c>&amp;&amp;</c> or <c>||</c>.</returns>
    private static bool IsConditional(SyntaxKind kind)
        => kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression;

    /// <summary>Returns the arithmetic precedence family of an operator, or <see cref="NoFamily"/>.</summary>
    /// <param name="kind">The operator kind.</param>
    /// <returns>The family rank.</returns>
    private static int ArithmeticFamily(SyntaxKind kind) => kind switch
    {
        SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression => MultiplicativeFamily,
        SyntaxKind.AddExpression or SyntaxKind.SubtractExpression => AdditiveFamily,
        SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression => ShiftFamily,
        _ => NoFamily,
    };
}
