// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.SealAttributeTypes);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(AttributeMetadataName) is not { } attributeType)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, attributeType), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports PSH1401 for an unsealed, non-abstract class deriving from <c>System.Attribute</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="attributeType">The resolved <c>System.Attribute</c> symbol.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol attributeType)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;
        if (symbol.TypeKind != TypeKind.Class
            || symbol.IsSealed
            || symbol.IsAbstract
            || !DerivesFromAttribute(symbol, attributeType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ApiSelectionRules.SealAttributeTypes, symbol.Locations[0], symbol.Name));
    }

    /// <summary>Returns whether a class derives (directly or indirectly) from <c>System.Attribute</c>.</summary>
    /// <param name="symbol">The class symbol to inspect.</param>
    /// <param name="attributeType">The resolved <c>System.Attribute</c> symbol.</param>
    /// <returns><see langword="true"/> when the base-type chain contains <c>System.Attribute</c>.</returns>
    private static bool DerivesFromAttribute(INamedTypeSymbol symbol, INamedTypeSymbol attributeType)
    {
        for (var baseType = symbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, attributeType))
            {
                return true;
            }
        }

        return false;
    }
}
