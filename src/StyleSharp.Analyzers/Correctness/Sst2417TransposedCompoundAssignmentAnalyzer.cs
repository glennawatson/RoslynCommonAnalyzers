// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an assignment spaced like a transposed operator (SST2417): <c>x =+ 1</c>, where the <c>=</c>
/// touches a unary <c>+</c>, <c>-</c> or <c>!</c> that is then followed by a space. The three tokens are the
/// same as the deliberate <c>x = +1</c> and differ only in where the space falls, so the spacing asymmetry
/// is the entire signal.
/// </summary>
/// <remarks>
/// Detection is purely lexical — no semantic model — so the clean path is a syntax-kind test and two trivia
/// reads. The companion spacing rules stay silent on the reported span so their fix does not close the gap
/// and cement the fake compound operator.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2417TransposedCompoundAssignmentAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.TransposedCompoundAssignment);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleAssignmentExpression);
    }

    /// <summary>Reports one assignment spaced like a transposed operator.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (!TransposedCompoundAssignment.Matches(assignment))
        {
            return;
        }

        var operatorToken = ((PrefixUnaryExpressionSyntax)assignment.Right).OperatorToken;
        var span = TextSpan.FromBounds(assignment.OperatorToken.SpanStart, operatorToken.Span.End);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.TransposedCompoundAssignment,
            assignment.SyntaxTree,
            span,
            operatorToken.Text));
    }
}
