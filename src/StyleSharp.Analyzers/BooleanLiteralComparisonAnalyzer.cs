// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a comparison of a boolean expression to a <c>true</c>/<c>false</c> literal
/// (SST1143) — <c>x == true</c>, <c>x != false</c>, and so on — which is always redundant.
/// The check is purely syntactic: when exactly one side of an equality is a boolean literal,
/// the other side is already a boolean, so no semantic model is needed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BooleanLiteralComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.NoBooleanLiteralComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Returns whether an expression is the <c>true</c> or <c>false</c> literal.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> for a boolean literal.</returns>
    internal static bool IsBooleanLiteral(ExpressionSyntax expression)
        => expression.IsKind(SyntaxKind.TrueLiteralExpression) || expression.IsKind(SyntaxKind.FalseLiteralExpression);

    /// <summary>Reports SST1143 when exactly one side of an equality is a boolean literal.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        var leftLiteral = IsBooleanLiteral(binary.Left);
        var rightLiteral = IsBooleanLiteral(binary.Right);
        if (leftLiteral == rightLiteral)
        {
            return;
        }

        var literal = leftLiteral ? binary.Left : binary.Right;
        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoBooleanLiteralComparison, binary.GetLocation(), literal.ToString()));
    }
}
