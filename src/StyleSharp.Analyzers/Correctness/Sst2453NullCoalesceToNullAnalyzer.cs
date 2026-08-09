// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a null-coalescing <c>a ?? b</c> whose right operand is a compile-time constant null (SST2453):
/// the fallback substitutes null for null, so the whole expression is its left operand.
/// </summary>
/// <remarks>
/// <para>
/// The coalescing's own type must equal the left operand's. That guard is what keeps
/// <c>nullableInt ?? default</c> out of the rule: there the right operand unwraps the nullable, so the
/// expression is not the left operand and folding to it would change the type.
/// </para>
/// <para>
/// A left operand that is itself a constant null belongs to the rule that folds a coalescing to its right
/// operand, so <c>null ?? null</c> is reported once, not twice.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2453NullCoalesceToNullAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.NullCoalesceToNull);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CoalesceExpression);
    }

    /// <summary>Returns whether an expression's shape allows it to be a compile-time constant.</summary>
    /// <param name="expression">The operand to inspect.</param>
    /// <returns><see langword="true"/> when the operand could bind to a constant null.</returns>
    internal static bool CouldBeConstant(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax => true,
        DefaultExpressionSyntax => true,
        IdentifierNameSyntax => true,
        MemberAccessExpressionSyntax => true,
        ParenthesizedExpressionSyntax parenthesized => CouldBeConstant(parenthesized.Expression),
        CastExpressionSyntax cast => CouldBeConstant(cast.Expression),
        _ => false,
    };

    /// <summary>Reports one <c>??</c> whose right operand is a constant null.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var coalesce = (BinaryExpressionSyntax)context.Node;
        if (!CouldBeConstant(coalesce.Right))
        {
            return;
        }

        var model = context.SemanticModel;
        var constant = model.GetConstantValue(coalesce.Right, context.CancellationToken);
        if (!constant.HasValue || constant.Value is not null)
        {
            return;
        }

        if (CouldBeConstant(coalesce.Left))
        {
            var left = model.GetConstantValue(coalesce.Left, context.CancellationToken);
            if (left is { HasValue: true, Value: null })
            {
                return;
            }
        }

        var wholeType = model.GetTypeInfo(coalesce, context.CancellationToken).Type;
        var leftType = model.GetTypeInfo(coalesce.Left, context.CancellationToken).Type;
        if (wholeType is null || leftType is null || !SymbolEqualityComparer.Default.Equals(wholeType, leftType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.NullCoalesceToNull,
            coalesce.SyntaxTree,
            coalesce.Span));
    }
}
