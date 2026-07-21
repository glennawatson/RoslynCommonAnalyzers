// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a wrapped binary operator that sits on the wrong side of its line break (SST1526),
/// configured with <c>stylesharp.binary_operator_new_line</c> (<c>before</c> | <c>after</c>; default
/// <c>before</c>). Each binary node's own operator is checked, so a whole chain is covered link by link
/// with no double report, and a single-line binary expression is never touched.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1526BinaryOperatorNewLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Rule-specific editorconfig key for the operator placement (SST1526).</summary>
    internal const string SpecificKey = "stylesharp.SST1526.binary_operator_new_line";

    /// <summary>General editorconfig key for the binary operator placement.</summary>
    internal const string GeneralKey = "stylesharp.binary_operator_new_line";

    /// <summary>The binary expression kinds whose wrapped operator placement is checked.</summary>
    private static readonly ImmutableArray<SyntaxKind> BinaryKinds = ImmutableArrays.Of(
        SyntaxKind.AddExpression,
        SyntaxKind.SubtractExpression,
        SyntaxKind.MultiplyExpression,
        SyntaxKind.DivideExpression,
        SyntaxKind.ModuloExpression,
        SyntaxKind.LeftShiftExpression,
        SyntaxKind.RightShiftExpression,
        SyntaxKind.UnsignedRightShiftExpression,
        SyntaxKind.LogicalOrExpression,
        SyntaxKind.LogicalAndExpression,
        SyntaxKind.BitwiseOrExpression,
        SyntaxKind.BitwiseAndExpression,
        SyntaxKind.ExclusiveOrExpression,
        SyntaxKind.EqualsExpression,
        SyntaxKind.NotEqualsExpression,
        SyntaxKind.LessThanExpression,
        SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.CoalesceExpression);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.BinaryOperatorNewLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, BinaryKinds);
    }

    /// <summary>Reports a wrapped binary operator on the wrong side of its line break.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        var op = binary.OperatorToken;
        var breakBefore = LayoutHelpers.HasLineBreakBefore(op);
        var breakAfter = LayoutHelpers.HasLineBreakAfter(op);
        if (!breakBefore && !breakAfter)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(binary.SyntaxTree);
        var wantBreakBefore = LayoutStyleOptions.ReadBreakBefore(options, SpecificKey, GeneralKey, defaultBreakBefore: true);
        if (wantBreakBefore ? !breakAfter : !breakBefore)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            LayoutRules.BinaryOperatorNewLine,
            op.GetLocation(),
            LayoutHelpers.PlacementProperties(wantBreakBefore),
            op.Text,
            wantBreakBefore ? "start" : "end"));
    }
}
