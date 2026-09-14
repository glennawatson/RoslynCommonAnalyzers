// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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

        context.RegisterCompilationStartAction(static start =>
        {
            var guid = new GuidSymbol(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, guid),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Reports one parameterless construction of a <c>Guid</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="guid">The <c>System.Guid</c> symbol resolved on first demand for this compilation.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, GuidSymbol guid)
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

    /// <summary>Binds a directly declared target type before falling back to binding the construction.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The parameterless construction to inspect.</param>
    /// <returns>The created type, or null when syntax excludes Guid.</returns>
    private static ITypeSymbol? GetCreatedType(in SyntaxNodeAnalysisContext context, BaseObjectCreationExpressionSyntax creation)
    {
        var typeSyntax = GetCreationTypeSyntax(creation);
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
        return creation is ImplicitObjectCreationExpressionSyntax
            && created is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
            ? context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type
            : created;
    }

    /// <summary>Finds a directly declared construction type without binding its surrounding expression.</summary>
    /// <param name="creation">The construction whose type is needed.</param>
    /// <returns>The explicit or declared type, or null when the surrounding expression determines it.</returns>
    private static TypeSyntax? GetCreationTypeSyntax(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax explicitObject => explicitObject.Type,
        { Parent: EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } } => declaration.Type,
        { Parent: EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } } => property.Type,
        _ => null,
    };

    /// <summary>Resolves the framework Guid symbol only after a matching construction is found.</summary>
    /// <param name="compilation">The compilation whose framework symbol is cached.</param>
    private sealed class GuidSymbol(Compilation compilation)
    {
        /// <summary>Serializes the first metadata lookup across callbacks.</summary>
        private readonly object _gate = new();

        /// <summary>The published result, including a missing framework type.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the framework type, resolving it once on first demand.</summary>
        /// <returns>The Guid symbol, or null when it cannot be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (Volatile.Read(ref _resolved) ?? Resolve())[0];

        /// <summary>Resolves and publishes the framework type once per compilation.</summary>
        /// <returns>The cached resolution result.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol?[] Resolve()
        {
            lock (_gate)
            {
                var resolved = _resolved;
                if (resolved is null)
                {
                    resolved = [compilation.GetTypeByMetadataName(GuidMetadataName)];
                    Volatile.Write(ref _resolved, resolved);
                }

                return resolved;
            }
        }
    }
}
