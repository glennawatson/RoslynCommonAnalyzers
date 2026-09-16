// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the parameterless <c>new Guid()</c> (SST2012). It does not make a new GUID — it makes the all-zero
/// one — and <c>Guid.Empty</c> is the name of that value. <c>Guid.NewGuid()</c> and every seeded constructor
/// are left alone: they mean what they say.
/// </summary>
/// <remarks>
/// Explicit constructions are gated on the name Guid. Target-typed constructions in variable and property
/// initializers bind the declared type, preserving aliases without binding the whole construction. Other
/// target-typed contexts require the construction's type information. Metadata is resolved only for a
/// surviving Guid candidate and cached once per compilation.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2012UseGuidEmptyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the constructed type.</summary>
    internal const string GuidMetadataName = "System.Guid";

    /// <summary>The type name used by the syntax gate.</summary>
    private const string GuidTypeName = "Guid";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ModernizationRules.UseGuidEmpty);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<INamedTypeSymbol?>(
                compilation,
                static target => target.GetTypeByMetadataName(GuidMetadataName),
                runOnce: true),
            Analyze,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Reports one parameterless construction of a <c>Guid</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="guid">The <c>System.Guid</c> symbol resolved on first demand for this compilation.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, LazyCompilationValue<INamedTypeSymbol?> guid)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: 0 } || creation.Initializer is not null)
        {
            return;
        }

        if (creation is ObjectCreationExpressionSyntax explicitCreation && SyntaxNames.GetIdentifierName(explicitCreation.Type) != GuidTypeName)
        {
            return;
        }

        var created = GetCreatedType(context, creation);
        if (created is not INamedTypeSymbol { Name: GuidTypeName, Arity: 0 }
            || guid.Get() is not { } guidType
            || !SymbolEqualityComparer.Default.Equals(created, guidType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.UseGuidEmpty, creation.GetLocation()));
    }

    /// <summary>Binds a target-typed construction's declared type before falling back to binding the construction.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The parameterless construction to inspect.</param>
    /// <returns>The created type, or null when syntax excludes Guid.</returns>
    private static ITypeSymbol? GetCreatedType(in SyntaxNodeAnalysisContext context, BaseObjectCreationExpressionSyntax creation)
    {
        // A written type is already part of the construction's bound node, so binding the construction reuses the
        // enclosing member's bound tree; binding the type on its own builds a second one.
        var typeSyntax = creation is ImplicitObjectCreationExpressionSyntax ? GetDeclaredTargetTypeSyntax(creation) : null;
        if (typeSyntax is null or NullableTypeSyntax)
        {
            return context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
        }

        if (typeSyntax is not NameSyntax || typeSyntax is GenericNameSyntax { Identifier.ValueText: not "Nullable" })
        {
            return null;
        }

        // An alias can denote Nullable<Guid>; target-typed new then constructs its underlying Guid.
        var created = context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken).Type;
        return created is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
            ? context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type
            : created;
    }

    /// <summary>Finds the declared type a target-typed construction takes, without binding its surrounding expression.</summary>
    /// <param name="creation">The construction whose type is needed.</param>
    /// <returns>The declared type, or null when the surrounding expression determines it.</returns>
    private static TypeSyntax? GetDeclaredTargetTypeSyntax(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        { Parent: EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } } => declaration.Type,
        { Parent: EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } } => property.Type,
        _ => null,
    };
}
