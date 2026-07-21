// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a null-coalescing <c>a ?? b</c> whose left operand is a compile-time constant null (SST2494):
/// the null literal, a <c>default</c> of a reference or nullable type, or a constant whose value is null.
/// The coalescing can never keep the left, so the whole expression is its right operand.
/// </summary>
/// <remarks>
/// <para>
/// A <c>??=</c> is not examined: its left operand must be an assignable location, which a compile-time
/// constant null can never be, so the shape cannot occur in code that compiles.
/// </para>
/// <para>
/// The clean path rejects on syntax — a left operand whose shape cannot be a constant (a call, a
/// <c>new</c>, an index) is dropped without binding — and only a constant-shaped left operand is asked
/// for its constant value.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2494ConstantNullCoalesceAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.ConstantNullCoalesce);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CoalesceExpression);
    }

    /// <summary>Reports one <c>??</c> whose left operand is a constant null.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var coalesce = (BinaryExpressionSyntax)context.Node;
        if (!CouldBeConstant(coalesce.Left))
        {
            return;
        }

        var constant = context.SemanticModel.GetConstantValue(coalesce.Left, context.CancellationToken);
        if (!constant.HasValue || constant.Value is not null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ConstantNullCoalesce,
            coalesce.SyntaxTree,
            coalesce.Span));
    }

    /// <summary>Returns whether an expression's shape allows it to be a compile-time constant.</summary>
    /// <param name="expression">The left operand to inspect.</param>
    /// <returns><see langword="true"/> when the operand could bind to a constant null.</returns>
    /// <remarks>
    /// A literal (<c>null</c>, <c>default</c>), a <c>default(T)</c>, a name that can be a <c>const</c>, and a
    /// cast or parenthesization of those can all fold. A call, a <c>new</c>, an index and everything else cannot,
    /// and is rejected before the semantic model is asked anything.
    /// </remarks>
    private static bool CouldBeConstant(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax => true,
        DefaultExpressionSyntax => true,
        IdentifierNameSyntax => true,
        MemberAccessExpressionSyntax => true,
        ParenthesizedExpressionSyntax parenthesized => CouldBeConstant(parenthesized.Expression),
        CastExpressionSyntax cast => CouldBeConstant(cast.Expression),
        _ => false,
    };
}
