// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires parentheses when arithmetic/shift operators of different precedence are
/// mixed (SST1407) and when <c>&amp;&amp;</c> and <c>||</c> are mixed (SST1408). The
/// inner sub-expression that should be parenthesized is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrecedenceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The sentinel for an operator that is not part of the precedence rules.</summary>
    private const int NoCategory = 0;

    /// <summary>The precedence category for <c>&amp;&amp;</c>.</summary>
    private const int ConditionalAndCategory = 1;

    /// <summary>The precedence category for <c>||</c>.</summary>
    private const int ConditionalOrCategory = 2;

    /// <summary>The precedence category for multiplicative operators.</summary>
    private const int MultiplicativeCategory = 3;

    /// <summary>The precedence category for additive operators.</summary>
    private const int AdditiveCategory = 4;

    /// <summary>The precedence category for shift operators.</summary>
    private const int ShiftCategory = 5;

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

    /// <summary>The rules this analyzer can report.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> Rules = ImmutableArrays.Of(
        MaintainabilityRules.ArithmeticPrecedence,
        MaintainabilityRules.ConditionalPrecedence);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Returns the rule a parent/child operator pair violates, or <see langword="null"/> when none.</summary>
    /// <param name="inner">The inner operator kind.</param>
    /// <param name="parent">The parent operator kind.</param>
    /// <returns>The violated rule, or <see langword="null"/>.</returns>
    internal static DiagnosticDescriptor? SelectRule(SyntaxKind inner, SyntaxKind parent)
    {
        var innerCategory = ClassifyOperator(inner);
        var parentCategory = ClassifyOperator(parent);
        if (innerCategory == parentCategory)
        {
            return null;
        }

        return innerCategory switch
        {
            ConditionalAndCategory or ConditionalOrCategory => parentCategory is ConditionalAndCategory
                or ConditionalOrCategory
                ? MaintainabilityRules.ConditionalPrecedence
                : null,
            _ => innerCategory >= MultiplicativeCategory && parentCategory >= MultiplicativeCategory
                ? MaintainabilityRules.ArithmeticPrecedence
                : null
        };
    }

    /// <summary>Classifies an operator kind into one precedence-rule category.</summary>
    /// <param name="kind">The operator kind.</param>
    /// <returns>The category id used for cheap precedence comparisons.</returns>
    [SuppressMessage(
        "Major Code Smell",
        "the rule:Cyclomatic Complexity of methods should not be too high",
        Justification = "A direct switch-based operator map is the lowest-overhead classification shape on the PrecedenceAnalyzer hot path.")]
    internal static int ClassifyOperator(SyntaxKind kind) => kind switch
    {
        SyntaxKind.LogicalAndExpression => ConditionalAndCategory,
        SyntaxKind.LogicalOrExpression => ConditionalOrCategory,
        SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression => MultiplicativeCategory,
        SyntaxKind.AddExpression or SyntaxKind.SubtractExpression => AdditiveCategory,
        SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression => ShiftCategory,
        _ => NoCategory
    };

    /// <summary>Reports an inner binary expression whose precedence against its parent is implicit.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var inner = (BinaryExpressionSyntax)context.Node;
        if (inner.Parent is not BinaryExpressionSyntax parent)
        {
            return;
        }

        var rule = SelectRule(inner.Kind(), parent.Kind());
        if (rule is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, inner.GetLocation()));
    }
}
