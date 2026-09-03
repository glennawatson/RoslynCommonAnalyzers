// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a conditional expression with exactly one boolean-literal branch (SST2288):
/// <c>a ? b : false</c> is <c>a &amp;&amp; b</c>, and <c>a ? true : b</c> is <c>a || b</c>.
/// </summary>
/// <remarks>
/// The whole match is syntactic and the rewrite text is only built once a branch is known to be a literal,
/// so a conditional that is not this shape costs one pattern match.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2288UseLogicalOperatorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ModernSyntaxRules.UseLogicalOperatorOverConditional);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
    }

    /// <summary>Classifies a conditional whose branches include exactly one boolean literal.</summary>
    /// <param name="conditional">The conditional expression.</param>
    /// <param name="negateCondition">Whether the rewrite negates the condition.</param>
    /// <param name="isConjunction">Whether the rewrite uses <c>&amp;&amp;</c> rather than <c>||</c>.</param>
    /// <param name="other">The branch that is not a literal.</param>
    /// <returns><see langword="true"/> when the conditional folds to a logical operator.</returns>
    /// <remarks>
    /// <c>a ? b : false</c> keeps the condition and conjoins; <c>a ? b : true</c> negates it and disjoins;
    /// <c>a ? true : b</c> keeps and disjoins; <c>a ? false : b</c> negates and conjoins. A conditional whose
    /// branches are both literals is left to the rule that collapses it to the condition.
    /// </remarks>
    internal static bool TryClassify(
        ConditionalExpressionSyntax conditional,
        out bool negateCondition,
        out bool isConjunction,
        out ExpressionSyntax other)
    {
        negateCondition = false;
        isConjunction = false;
        other = null!;

        var whenTrue = LiteralValue(conditional.WhenTrue);
        var whenFalse = LiteralValue(conditional.WhenFalse);
        if (whenTrue.HasValue == whenFalse.HasValue)
        {
            return false;
        }

        if (whenFalse is { } falseLiteral)
        {
            other = conditional.WhenTrue;
            isConjunction = !falseLiteral;
            negateCondition = falseLiteral;
            return true;
        }

        other = conditional.WhenFalse;
        var trueLiteral = whenTrue!.Value;
        isConjunction = !trueLiteral;
        negateCondition = !trueLiteral;
        return true;
    }

    /// <summary>Gets the value of a boolean literal branch.</summary>
    /// <param name="expression">The branch expression.</param>
    /// <returns>The literal's value, or <see langword="null"/> when the branch is not a boolean literal.</returns>
    private static bool? LiteralValue(ExpressionSyntax expression) => expression.Kind() switch
    {
        SyntaxKind.TrueLiteralExpression => true,
        SyntaxKind.FalseLiteralExpression => false,
        _ => null,
    };

    /// <summary>Reports one conditional that folds to a logical operator.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        if (!TryClassify(conditional, out var negate, out var conjunction, out var other))
        {
            return;
        }

        var condition = negate ? "!" + conditional.Condition : conditional.Condition.ToString();
        var suggestion = $"{condition} {(conjunction ? "&&" : "||")} {other}";
        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UseLogicalOperatorOverConditional,
            conditional.SyntaxTree,
            conditional.Span,
            suggestion));
    }
}
