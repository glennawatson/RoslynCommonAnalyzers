// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>x == null</c> / <c>x != null</c> where <c>x</c> is an unconstrained type parameter
/// (SST2493). With no <c>class</c>, <c>struct</c>, or <c>notnull</c> constraint the parameter can be a
/// value type, and there the operator resolves to a reference comparison that is always false — so the
/// check silently answers the wrong question. The constant pattern <c>is null</c> / <c>is not null</c>
/// is correct for every substitution and does not box.
/// </summary>
/// <remarks>
/// The clean path rejects on syntax first — an equality whose other operand is not the null literal is
/// dropped without binding — then binds the non-null operand once and reports only when its type is a
/// type parameter that could be substituted with a value type.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2493NullComparisonOnUnconstrainedGenericAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.NullComparisonOnUnconstrainedGeneric);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Reports one null comparison whose non-null operand is an unconstrained type parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (GetOperandComparedToNull(binary) is not { } operand)
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(operand, context.CancellationToken).Type is not ITypeParameterSymbol typeParameter
            || !CanBeValueType(typeParameter))
        {
            return;
        }

        var isNotEquals = binary.IsKind(SyntaxKind.NotEqualsExpression);
        var operandText = operand.ToString();
        var written = $"{operandText} {binary.OperatorToken.Text} null";
        var suggested = isNotEquals ? $"{operandText} is not null" : $"{operandText} is null";

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.NullComparisonOnUnconstrainedGeneric,
            binary.GetLocation(),
            typeParameter.Name,
            written,
            suggested));
    }

    /// <summary>Returns the non-null operand of an equality whose other operand is the null literal.</summary>
    /// <param name="binary">The equality expression.</param>
    /// <returns>The non-null operand, or <see langword="null"/> when neither operand is the null literal.</returns>
    private static ExpressionSyntax? GetOperandComparedToNull(BinaryExpressionSyntax binary)
    {
        if (binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return binary.Left;
        }

        return binary.Left.IsKind(SyntaxKind.NullLiteralExpression) ? binary.Right : null;
    }

    /// <summary>Returns whether a type parameter could be substituted with a value type.</summary>
    /// <param name="typeParameter">The type parameter to inspect.</param>
    /// <returns><see langword="true"/> when nothing constrains the parameter to a reference type.</returns>
    /// <remarks>
    /// A <c>class</c>, <c>struct</c>, <c>notnull</c>, or <c>unmanaged</c> constraint settles the question, and a
    /// class base-type constraint pins the parameter to a reference type. An interface constraint does not — a
    /// value type can implement it — so it is not enough to make <c>== null</c> meaningful.
    /// </remarks>
    private static bool CanBeValueType(ITypeParameterSymbol typeParameter)
    {
        if (typeParameter.HasReferenceTypeConstraint
            || typeParameter.HasValueTypeConstraint
            || typeParameter.HasNotNullConstraint
            || typeParameter.HasUnmanagedTypeConstraint)
        {
            return false;
        }

        var constraintTypes = typeParameter.ConstraintTypes;
        for (var i = 0; i < constraintTypes.Length; i++)
        {
            if (constraintTypes[i].TypeKind == TypeKind.Class)
            {
                return false;
            }
        }

        return true;
    }
}
