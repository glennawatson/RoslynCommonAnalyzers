// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags casts that push a value type through <c>object</c> and immediately back, like
/// <c>(int)(object)dayOfWeek</c> (PSH1015). The intermediate cast allocates a box that is
/// discarded as soon as it is unboxed; a direct cast converts with no allocation. Only
/// concrete value types on both ends are reported — casts written on type parameters are the
/// well-known generic specialization pattern whose box the JIT already elides — and only when
/// a direct, non-user-defined conversion exists so removing the intermediate cast compiles
/// and keeps the same semantics.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1015BoxingRoundTripCastAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.BoxingRoundTripCast);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeCast, SyntaxKind.CastExpression);
    }

    /// <summary>Returns the intermediate object cast of a round-trip cast shape, before any binding.</summary>
    /// <param name="cast">The outer cast to inspect.</param>
    /// <returns>The inner cast to object, or <see langword="null"/> when the shape does not match.</returns>
    internal static CastExpressionSyntax? TryGetObjectCast(CastExpressionSyntax cast)
    {
        var operand = cast.Expression;
        while (operand is ParenthesizedExpressionSyntax parenthesized)
        {
            operand = parenthesized.Expression;
        }

        return operand is CastExpressionSyntax inner && IsObjectType(inner.Type) ? inner : null;
    }

    /// <summary>Returns whether a type syntax spells the object type.</summary>
    /// <param name="type">The cast target type syntax.</param>
    /// <returns><see langword="true"/> for the object keyword or an Object simple name.</returns>
    private static bool IsObjectType(TypeSyntax type)
        => type switch
        {
            PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword),
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == nameof(Object),
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == nameof(Object),
            _ => false,
        };

    /// <summary>Reports PSH1015 for a value-to-value cast routed through an object box.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeCast(SyntaxNodeAnalysisContext context)
    {
        var cast = (CastExpressionSyntax)context.Node;
        if (TryGetObjectCast(cast) is not { } objectCast)
        {
            return;
        }

        var model = context.SemanticModel;
        var sourceType = model.GetTypeInfo(objectCast.Expression, context.CancellationToken).Type;
        var targetType = model.GetTypeInfo(cast.Type, context.CancellationToken).Type;
        if (!IsConcreteValueType(sourceType) || !IsConcreteValueType(targetType))
        {
            return;
        }

        var conversion = model.Compilation.ClassifyConversion(sourceType!, targetType!);
        if (!conversion.Exists || conversion.IsUserDefined)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.BoxingRoundTripCast,
            cast.SyntaxTree,
            cast.Span,
            sourceType!.Name));
    }

    /// <summary>Returns whether a type is a concrete value type rather than a type parameter.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for structs and enums that are not type parameters.</returns>
    private static bool IsConcreteValueType(ITypeSymbol? type)
        => type is { IsValueType: true } and not ITypeParameterSymbol;
}
