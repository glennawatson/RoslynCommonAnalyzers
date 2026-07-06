// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports negated pattern expressions that can be written with <c>is not</c>. The analyzer is
/// syntax-only and skips declaration patterns because moving a declaration under a negative
/// pattern changes where a variable can be used.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2008IsNotPatternAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.UseIsNotPattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LogicalNotExpression);
    }

    /// <summary>Reports <c>!(value is pattern)</c> when the pattern has no declaration.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // The fix emits an 'is not' pattern, which requires C# 9.
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp9 })
        {
            return;
        }

        var notExpression = (PrefixUnaryExpressionSyntax)context.Node;
        if (Unwrap(notExpression.Operand) is not IsPatternExpressionSyntax isPattern || ContainsDeclaration(isPattern.Pattern))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.UseIsNotPattern, notExpression.GetLocation()));
    }

    /// <summary>Removes enclosing parentheses around an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The unwrapped expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Returns whether a pattern declares a local variable.</summary>
    /// <param name="pattern">The pattern to inspect.</param>
    /// <returns><see langword="true"/> when rewriting would affect variable availability.</returns>
    private static bool ContainsDeclaration(PatternSyntax pattern)
    {
        if (pattern is DeclarationPatternSyntax)
        {
            return true;
        }

        foreach (var child in pattern.DescendantNodes())
        {
            if (child is DeclarationPatternSyntax)
            {
                return true;
            }
        }

        return false;
    }
}
