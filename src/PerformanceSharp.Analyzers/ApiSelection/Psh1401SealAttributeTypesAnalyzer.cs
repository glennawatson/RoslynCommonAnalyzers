// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports unsealed, non-abstract attribute classes (PSH1401). Reflection-based
/// attribute lookups are cheaper on sealed attribute types because the runtime never
/// has to consider derived attributes. The rule reports declarations, not usages, so
/// sealing a type that is subclassed in another assembly is the author's call — see
/// the rule docs for the breaking-change caveat.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1401SealAttributeTypesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the attribute base type.</summary>
    private const string AttributeMetadataName = "System.Attribute";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ApiSelectionRules.SealAttributeTypes);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSymbolAction(
            context,
            static compilation => new LazyCompilationValue<INamedTypeSymbol?>(compilation, ResolveAttributeType, runOnce: true),
            AnalyzeNamedType,
            SymbolKind.NamedType);
    }

    /// <summary>Reports PSH1401 for an unsealed, non-abstract class deriving from <c>System.Attribute</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="attributeType">The attribute base type, resolved only for an eligible class.</param>
    private static void AnalyzeNamedType(in SymbolAnalysisContext context, LazyCompilationValue<INamedTypeSymbol?> attributeType)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;
        if (symbol.TypeKind != TypeKind.Class
            || symbol.IsSealed
            || symbol.IsAbstract
            || symbol.BaseType is null
            || symbol.BaseType.SpecialType == SpecialType.System_Object
            || !DerivesFromAttribute(symbol, attributeType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ApiSelectionRules.SealAttributeTypes, symbol.Locations[0], symbol.Name));
    }

    /// <summary>Returns whether a class derives (directly or indirectly) from <c>System.Attribute</c>.</summary>
    /// <param name="symbol">The class symbol to inspect.</param>
    /// <param name="attributeType">The attribute base type, resolved only for a matching base name.</param>
    /// <returns><see langword="true"/> when the base-type chain contains <c>System.Attribute</c>.</returns>
    private static bool DerivesFromAttribute(INamedTypeSymbol symbol, LazyCompilationValue<INamedTypeSymbol?> attributeType)
    {
        for (var baseType = symbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.MetadataName == "Attribute"
                && attributeType.Get() is { } resolvedType
                && SymbolEqualityComparer.Default.Equals(baseType, resolvedType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the attribute base type.</summary>
    /// <param name="compilation">The compilation whose metadata is resolved.</param>
    /// <returns>The attribute base type, or null when it is unavailable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static INamedTypeSymbol? ResolveAttributeType(Compilation compilation) =>
        compilation.GetTypeByMetadataName(AttributeMetadataName);
}
