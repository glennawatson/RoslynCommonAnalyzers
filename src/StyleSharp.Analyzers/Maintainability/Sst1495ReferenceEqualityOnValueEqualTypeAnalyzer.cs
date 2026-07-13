// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>==</c> and <c>!=</c> on a reference type that overrides <c>Equals(object)</c> but does not
/// overload the operator (SST1495). The operator then compares references while <c>Equals</c> compares
/// values, so the two answer differently for the same pair.
/// </summary>
/// <remarks>
/// <para>
/// A type that overloads <c>==</c> is never reported: the operator means whatever its author defined, and
/// that includes <c>string</c>, every <c>record</c>, and the delegate types, all of which the compiler or
/// the framework has already given an operator. A value type cannot be compared by reference, and a type
/// parameter has no operator to reason about, so neither is reported. Comparing against <c>null</c> is a
/// null check and is always right, and an operand typed as <c>object</c> is an explicit request for
/// reference semantics.
/// </para>
/// <para>
/// The clean path is a syntax test: a comparison with a literal operand — the <c>null</c> check above all —
/// is rejected before the semantic model is touched.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1495ReferenceEqualityOnValueEqualTypeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(MaintainabilityRules.ReferenceEqualityOnValueEqualType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Returns whether a comparison compares references while the type compares values.</summary>
    /// <param name="type">The operand type.</param>
    /// <returns><see langword="true"/> when the type overrides Equals and leaves the operator alone.</returns>
    internal static bool IsValueEqualWithoutOperator(ITypeSymbol type)
        => OverridesObjectEquals(type) && !HasEqualityOperator(type);

    /// <summary>Reports a reference comparison of a type that defines value equality.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;

        // A literal operand is either the null check this rule must not touch or a value the framework
        // types already give an operator, so the whole clean path stops here without a bind.
        if (IsLiteral(comparison.Left) || IsLiteral(comparison.Right))
        {
            return;
        }

        var model = context.SemanticModel;
        var leftType = model.GetTypeInfo(comparison.Left, context.CancellationToken).Type;
        if (!IsComparableReferenceType(leftType)
            || !IsComparableReferenceType(model.GetTypeInfo(comparison.Right, context.CancellationToken).Type)
            || !IsValueEqualWithoutOperator(leftType!))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.ReferenceEqualityOnValueEqualType,
            comparison.GetLocation(),
            leftType!.Name));
    }

    /// <summary>Returns whether an operand is a literal, which covers <c>null</c> and <c>default</c>.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns><see langword="true"/> when the operand states a value rather than naming one.</returns>
    private static bool IsLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax or DefaultExpressionSyntax;

    /// <summary>Returns whether an operand's type can meaningfully be compared by reference.</summary>
    /// <param name="type">The operand type.</param>
    /// <returns><see langword="true"/> when the type is a concrete reference type the author controls the equality of.</returns>
    /// <remarks>
    /// An interface says nothing about how the implementation defines equality; a type parameter has no
    /// operator to look at; <c>object</c> is how an author asks for reference semantics on purpose. A record
    /// carries a compiler-written operator and is filtered here so the operator walk never runs for it.
    /// </remarks>
    private static bool IsComparableReferenceType(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { IsRecord: true })
        {
            return false;
        }

        return type is
        {
            IsReferenceType: true,
            SpecialType: not SpecialType.System_Object,
            TypeKind: not TypeKind.Interface and not TypeKind.TypeParameter and not TypeKind.Dynamic and not TypeKind.Error,
            IsAnonymousType: false,
        };
    }

    /// <summary>Returns whether a type, or a base of it, overrides <c>Equals(object)</c>.</summary>
    /// <param name="type">The operand type.</param>
    /// <returns><see langword="true"/> when the type has said what equality means for it.</returns>
    private static bool OverridesObjectEquals(ITypeSymbol type)
    {
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            var candidates = current.GetMembers("Equals");
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IMethodSymbol { IsOverride: true, Parameters.Length: 1 } method
                    && method.Parameters[0].Type.SpecialType == SpecialType.System_Object)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a type, or a base of it, overloads <c>==</c>.</summary>
    /// <param name="type">The operand type.</param>
    /// <returns><see langword="true"/> when the operator means whatever its author defined.</returns>
    private static bool HasEqualityOperator(ITypeSymbol type)
    {
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            if (current.GetMembers(WellKnownMemberNames.EqualityOperatorName).Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
