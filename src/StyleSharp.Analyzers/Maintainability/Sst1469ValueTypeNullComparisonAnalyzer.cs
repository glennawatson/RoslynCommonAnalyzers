// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports equality and inequality comparisons between a non-nullable value type and the null
/// literal. Such comparisons compile only through a lifted operator, so they fold to the same
/// constant on every execution and usually point at a misread type or a missing '?'. Nullable
/// value types, pointers, and type parameters are skipped because null can be meaningful there.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1469ValueTypeNullComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.ValueTypeNullComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Reports null comparisons whose other operand is a non-nullable value type.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryGetNonNullOperand(binary, out var operand))
        {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(operand, context.CancellationToken).Type;
        if (type is null || !IsNonNullableValueType(type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.ValueTypeNullComparison,
            binary.GetLocation(),
            type.ToMinimalDisplayString(context.SemanticModel, operand.SpanStart),
            binary.IsKind(SyntaxKind.EqualsExpression) ? "false" : "true"));
    }

    /// <summary>Finds the operand paired against a null literal, without touching the semantic model.</summary>
    /// <param name="binary">The comparison expression.</param>
    /// <param name="operand">The operand on the other side of the null literal.</param>
    /// <returns><see langword="true"/> when exactly one operand is the null literal.</returns>
    private static bool TryGetNonNullOperand(BinaryExpressionSyntax binary, out ExpressionSyntax operand)
    {
        var leftIsNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
        var rightIsNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
        if (leftIsNull == rightIsNull)
        {
            operand = binary;
            return false;
        }

        operand = leftIsNull ? binary.Right : binary.Left;
        return true;
    }

    /// <summary>Returns whether a type can never be null on either side of a lifted comparison.</summary>
    /// <param name="type">The operand type.</param>
    /// <returns><see langword="true"/> for non-nullable value types.</returns>
    private static bool IsNonNullableValueType(ITypeSymbol type)
        => type.IsValueType
            && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
            && type.TypeKind is not (TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.TypeParameter);
}
