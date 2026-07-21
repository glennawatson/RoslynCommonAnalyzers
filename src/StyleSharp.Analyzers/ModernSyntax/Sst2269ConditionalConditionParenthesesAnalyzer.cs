// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the condition of a conditional expression whose parentheses do not match the configured style
/// (SST2269): a parenthesized single simple token when <c>stylesharp.conditional_condition_parentheses</c> is
/// <c>omit_when_single_token</c> (the default), or an unparenthesized condition when it is <c>include</c>. The
/// rule is opt-in and off by default.
/// </summary>
/// <remarks>
/// Under the default, only a condition that is a single identifier or literal wrapped in parentheses is
/// reported — a parenthesized larger expression keeps its parentheses because they can group and clarify. The
/// option is read only after that shape is matched.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2269ConditionalConditionParenthesesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The reported target when the codebase omits single-token parentheses.</summary>
    internal const string OmitTarget = "omit";

    /// <summary>The reported target when the codebase includes the parentheses.</summary>
    internal const string IncludeTarget = "include";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.NormalizeConditionalConditionParentheses);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
    }

    /// <summary>Returns whether an expression is a single simple token — an identifier or a literal.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when the expression is one identifier or one literal.</returns>
    internal static bool IsSingleSimpleToken(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax or LiteralExpressionSyntax;

    /// <summary>Reports a conditional condition whose parentheses style does not match the configured one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var condition = ((ConditionalExpressionSyntax)context.Node).Condition;
        if (condition is ParenthesizedExpressionSyntax parenthesized)
        {
            if (!IsSingleSimpleToken(parenthesized.Expression))
            {
                return;
            }

            var style = ModernSyntaxStyleOptions.ReadConditionalConditionParentheses(context.Options.AnalyzerConfigOptionsProvider.GetOptions(condition.SyntaxTree));
            if (style != ConditionalConditionParenthesesStyle.OmitWhenSingleToken)
            {
                return;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeConditionalConditionParentheses, condition.GetLocation(), OmitTarget));
            return;
        }

        var includeStyle = ModernSyntaxStyleOptions.ReadConditionalConditionParentheses(context.Options.AnalyzerConfigOptionsProvider.GetOptions(condition.SyntaxTree));
        if (includeStyle != ConditionalConditionParenthesesStyle.Include)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeConditionalConditionParentheses, condition.GetLocation(), IncludeTarget));
    }
}
