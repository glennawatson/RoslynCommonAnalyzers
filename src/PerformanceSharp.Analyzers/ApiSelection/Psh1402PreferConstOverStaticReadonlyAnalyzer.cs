// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports non-public <c>static readonly</c> fields whose value is a compile-time
/// constant of a const-capable type (PSH1402). A <c>const</c> folds the value into
/// call sites instead of paying a field load on every use. Public and protected
/// fields are skipped because const values bake into consuming assemblies.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1402PreferConstOverStaticReadonlyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.PreferConstOverStaticReadonly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports PSH1402 for a static readonly field whose initializer is a compile-time constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!HasConstConvertibleModifiers(field.Modifiers)
            || field.Declaration.Variables is not [var variable]
            || variable.Initializer is not { } initializer)
        {
            return;
        }

        var fieldType = context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type;
        if (fieldType is null
            || !AdmitsConst(fieldType)
            || !context.SemanticModel.GetConstantValue(initializer.Value, context.CancellationToken).HasValue)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.PreferConstOverStaticReadonly,
            variable.SyntaxTree,
            variable.Identifier.Span,
            variable.Identifier.ValueText));
    }

    /// <summary>Returns whether a modifier list is <c>static readonly</c> without public exposure.</summary>
    /// <param name="modifiers">The modifier list to inspect.</param>
    /// <returns><see langword="true"/> when both <c>static</c> and <c>readonly</c> are present and neither <c>public</c> nor <c>protected</c> is.</returns>
    private static bool HasConstConvertibleModifiers(SyntaxTokenList modifiers)
    {
        var hasStatic = false;
        var hasReadonly = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            switch (modifiers[i].Kind())
            {
                case SyntaxKind.StaticKeyword:
                {
                    hasStatic = true;
                    break;
                }

                case SyntaxKind.ReadOnlyKeyword:
                {
                    hasReadonly = true;
                    break;
                }

                case SyntaxKind.PublicKeyword:
                case SyntaxKind.ProtectedKeyword:
                    return false;
            }
        }

        return hasStatic && hasReadonly;
    }

    /// <summary>Returns whether a type can be declared <c>const</c>.</summary>
    /// <param name="type">The field type to inspect.</param>
    /// <returns><see langword="true"/> for enum types and the const-capable special types.</returns>
    /// <remarks>
    /// <see cref="SpecialType.System_Boolean"/> through <see cref="SpecialType.System_String"/> is a
    /// contiguous run covering exactly the const-capable primitives: bool, char, the integral types,
    /// decimal, float, double, and string.
    /// </remarks>
    private static bool AdmitsConst(ITypeSymbol type)
        => type.TypeKind == TypeKind.Enum
            || type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_String;
}
