// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires parentheses when a binary expression is mixed with the null-coalescing operator
/// <c>??</c> (SST1418). Because <c>??</c> has lower precedence than every binary operator, the
/// ambiguity is always an un-parenthesized binary <em>operand</em> of the <c>??</c>; reporting
/// that operand mirrors how SST1407/SST1408 report the inner sub-expression.
/// </summary>
/// <remarks>
/// The type-testing operators are excluded. Roslyn models <c>x as T</c> as a binary expression, but
/// <c>x as T ?? fallback</c> is the idiom <c>as</c> exists for: <c>as</c> is the only thing the
/// <c>??</c> could bind to, there is no reading a parenthesis would settle, and the bracketed form is
/// the harder one to read. This rule is about the operators whose precedence against <c>??</c> a
/// reader could genuinely get wrong — the arithmetic, shift, comparison, and logical ones.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1418NullCoalescingPrecedenceAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NullCoalescingPrecedence);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CoalesceExpression);
    }

    /// <summary>Reports SST1418 for each binary operand of a <c>??</c> expression.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var coalesce = (BinaryExpressionSyntax)context.Node;
        ReportOperand(context, coalesce.Left);
        ReportOperand(context, coalesce.Right);
    }

    /// <summary>Reports an operand when it is an un-parenthesized binary expression other than <c>??</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="operand">A <c>??</c> operand.</param>
    private static void ReportOperand(SyntaxNodeAnalysisContext context, ExpressionSyntax operand)
    {
        if (operand is not BinaryExpressionSyntax binary
            || binary.IsKind(SyntaxKind.CoalesceExpression)
            || binary.IsKind(SyntaxKind.AsExpression)
            || binary.IsKind(SyntaxKind.IsExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NullCoalescingPrecedence, binary.GetLocation()));
    }
}
