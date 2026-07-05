// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports readonly fields whose type is a mutable source-defined struct (SST1456).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1456ReadonlyMutableStructFieldAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NoReadonlyMutableStructField);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports each readonly field declarator that stores a mutable struct.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!ModifierListHelper.Contains(field.Modifiers, SyntaxKind.ReadOnlyKeyword)
            || context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type is not INamedTypeSymbol type
            || !IsMutableSourceStruct(type))
        {
            return;
        }

        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            var identifier = variables[i].Identifier;
            context.ReportDiagnostic(Diagnostic.Create(
                MaintainabilityRules.NoReadonlyMutableStructField,
                identifier.GetLocation(),
                identifier.ValueText));
        }
    }

    /// <summary>Returns whether a type is a mutable struct declared in source.</summary>
    /// <param name="type">The type symbol.</param>
    /// <returns><see langword="true"/> for non-readonly source structs.</returns>
    private static bool IsMutableSourceStruct(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct
            && type.SpecialType == SpecialType.None
            && !type.IsReadOnly
            && type.DeclaringSyntaxReferences.Length > 0;
}
