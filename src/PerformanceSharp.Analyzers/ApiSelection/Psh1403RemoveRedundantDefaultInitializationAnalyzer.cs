// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports field initializers that restate the field type's default value (PSH1403).
/// The runtime zero-initializes fields before any constructor runs, so the explicit
/// assignment repeats that work in every constructor for nothing. Struct fields are
/// skipped entirely (struct field-initializer semantics depend on which constructor
/// runs), const fields are skipped (the initializer is mandatory), and a
/// null-forgiving <c>null!</c> is skipped because the annotation is intentional.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1403RemoveRedundantDefaultInitializationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.RemoveRedundantDefaultInitialization);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports PSH1403 for every declarator whose initializer restates the field type's default.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (IsDeclaredInStruct(field) || ModifierListHelper.Contains(field.Modifiers, SyntaxKind.ConstKeyword))
        {
            return;
        }

        ITypeSymbol? fieldType = null;
        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if (variable.Initializer is not { } initializer
                || !IsRedundantDefaultInitializer(context, field, initializer.Value, ref fieldType))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                ApiSelectionRules.RemoveRedundantDefaultInitialization,
                initializer.SyntaxTree,
                initializer.Span,
                variable.Identifier.ValueText));
        }
    }

    /// <summary>Returns whether one initializer value restates the field type's default.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="field">The declaring field.</param>
    /// <param name="value">The initializer value expression.</param>
    /// <param name="fieldType">The lazily resolved field type, cached across declarators.</param>
    /// <returns><see langword="true"/> when the initializer is redundant.</returns>
    private static bool IsRedundantDefaultInitializer(
        SyntaxNodeAnalysisContext context,
        FieldDeclarationSyntax field,
        ExpressionSyntax value,
        ref ITypeSymbol? fieldType)
    {
        if (CanNeverBeDefaultValue(value.Kind()) || IsNullForgiving(value))
        {
            return false;
        }

        if (value.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            return true;
        }

        fieldType ??= context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type;
        if (fieldType is null)
        {
            return false;
        }

        if (value is DefaultExpressionSyntax
            && SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetTypeInfo(value, context.CancellationToken).Type, fieldType))
        {
            return true;
        }

        var constant = context.SemanticModel.GetConstantValue(value, context.CancellationToken);
        return constant.HasValue && IsDefaultValue(fieldType, constant.Value);
    }

    /// <summary>Returns whether a field is declared inside a struct (or record struct) declaration.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> when the containing type is a struct.</returns>
    private static bool IsDeclaredInStruct(FieldDeclarationSyntax field)
        => field.Parent is { } parent
            && (parent.IsKind(SyntaxKind.StructDeclaration) || parent.IsKind(SyntaxKind.RecordStructDeclaration));

    /// <summary>Returns whether an initializer shape can never produce a constant default value.</summary>
    /// <param name="kind">The initializer expression kind.</param>
    /// <returns><see langword="true"/> when the semantic checks can be skipped.</returns>
    private static bool CanNeverBeDefaultValue(SyntaxKind kind)
        => kind is SyntaxKind.ObjectCreationExpression
            or SyntaxKind.ImplicitObjectCreationExpression
            or SyntaxKind.ArrayCreationExpression
            or SyntaxKind.ImplicitArrayCreationExpression
            or SyntaxKind.CollectionExpression;

    /// <summary>Returns whether an initializer is a null-forgiving expression such as <c>null!</c>.</summary>
    /// <param name="value">The initializer value expression.</param>
    /// <returns><see langword="true"/> when the value is suppressed with <c>!</c>.</returns>
    private static bool IsNullForgiving(ExpressionSyntax value)
    {
        var current = value;
        while (current is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized.Expression;
        }

        return current.IsKind(SyntaxKind.SuppressNullableWarningExpression);
    }

    /// <summary>Returns whether a constant equals the field type's default value.</summary>
    /// <param name="fieldType">The field type.</param>
    /// <param name="value">The boxed constant value.</param>
    /// <returns><see langword="true"/> when the constant is the type's default.</returns>
    private static bool IsDefaultValue(ITypeSymbol fieldType, object? value)
    {
        if (value is null)
        {
            return fieldType.IsReferenceType || fieldType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }

        if (fieldType.TypeKind == TypeKind.Enum)
        {
            return IsIntegralZero(value);
        }

        return IsDefaultForSpecialType(fieldType.SpecialType, value);
    }

    /// <summary>Returns whether a constant equals the default of a const-capable special type.</summary>
    /// <param name="specialType">The field type's special type.</param>
    /// <param name="value">The boxed constant value.</param>
    /// <returns><see langword="true"/> when the constant is the special type's default.</returns>
    private static bool IsDefaultForSpecialType(SpecialType specialType, object value)
        => specialType switch
        {
            SpecialType.System_Boolean => value is false,
            SpecialType.System_Char => value is '\0',
            SpecialType.System_Single or SpecialType.System_Double => IsPositiveZeroFloating(value),
            SpecialType.System_Decimal => IsDecimalZero(value),
            >= SpecialType.System_SByte and <= SpecialType.System_UInt64 => IsIntegralZero(value),
            _ => false,
        };

    /// <summary>Returns whether a constant is positive floating-point zero (negative zero is intentional).</summary>
    /// <param name="value">The boxed constant value.</param>
    /// <returns><see langword="true"/> for <c>+0.0</c> constants only.</returns>
    private static bool IsPositiveZeroFloating(object value)
        => value switch
        {
            float singleValue => BitConverter.DoubleToInt64Bits(singleValue) == 0,
            double doubleValue => BitConverter.DoubleToInt64Bits(doubleValue) == 0,
            _ => IsIntegralZero(value),
        };

    /// <summary>Returns whether a constant is decimal (or integral) zero.</summary>
    /// <param name="value">The boxed constant value.</param>
    /// <returns><see langword="true"/> when the constant is zero.</returns>
    private static bool IsDecimalZero(object value)
        => value is decimal decimalValue ? decimalValue == decimal.Zero : IsIntegralZero(value);

    /// <summary>Returns whether a boxed integral constant is zero.</summary>
    /// <param name="value">The boxed constant value.</param>
    /// <returns><see langword="true"/> when the constant is an integral zero.</returns>
    private static bool IsIntegralZero(object value)
        => value switch
        {
            int intValue => intValue == 0,
            sbyte sbyteValue => sbyteValue == 0,
            byte byteValue => byteValue == 0,
            short shortValue => shortValue == 0,
            ushort ushortValue => ushortValue == 0,
            uint uintValue => uintValue == 0,
            long longValue => longValue == 0,
            ulong ulongValue => ulongValue == 0,
            _ => false,
        };
}
