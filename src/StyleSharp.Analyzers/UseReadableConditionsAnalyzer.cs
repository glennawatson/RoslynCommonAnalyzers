// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a "yoda" comparison that places a constant on the left and a variable on the right
/// (<c>0 == count</c>), which most readers parse more slowly than the natural <c>count == 0</c>
/// (SST1131). Only flagged when exactly one side is a literal constant, so a swap is unambiguous.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseReadableConditionsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The comparison operators the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> ComparisonKinds = ImmutableArrays.Of(
        SyntaxKind.EqualsExpression,
        SyntaxKind.NotEqualsExpression,
        SyntaxKind.LessThanExpression,
        SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.UseReadableConditions);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, ComparisonKinds);
    }

    /// <summary>Reports a comparison whose constant operand is on the left.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (!IsConstantOperand(comparison.Left) || IsConstantOperand(comparison.Right))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseReadableConditions, comparison.GetLocation()));
    }

    /// <summary>Returns whether the expression is a literal constant (optionally signed).</summary>
    /// <param name="expression">The operand to test.</param>
    /// <returns><see langword="true"/> when the operand is a literal or a signed numeric literal.</returns>
    private static bool IsConstantOperand(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax => true,
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression or (int)SyntaxKind.UnaryPlusExpression, Operand: LiteralExpressionSyntax } => true,
        _ => false
    };
}
