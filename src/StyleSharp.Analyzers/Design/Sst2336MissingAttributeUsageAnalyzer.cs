// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a concrete attribute type that declares no <c>[AttributeUsage]</c> (SST2336), so it silently
/// accepts every target, cannot repeat, and is not inherited.
/// </summary>
/// <remarks>
/// <c>Attribute</c> and <c>AttributeUsageAttribute</c> are resolved once per compilation and the rule
/// registers nothing when they are absent, so a project with no attributes pays one lookup.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2336MissingAttributeUsageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the attribute base type.</summary>
    private const string AttributeMetadataName = "System.Attribute";

    /// <summary>The metadata name of the usage attribute.</summary>
    private const string AttributeUsageMetadataName = "System.AttributeUsageAttribute";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DesignRules.MissingAttributeUsage);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var attribute = start.Compilation.GetTypeByMetadataName(AttributeMetadataName);
            var usage = start.Compilation.GetTypeByMetadataName(AttributeUsageMetadataName);
            if (attribute is null || usage is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, attribute, usage), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports one attribute type with no declared usage.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="attribute">The resolved attribute base type.</param>
    /// <param name="usage">The resolved usage attribute type.</param>
    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attribute, INamedTypeSymbol usage)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } type
            || !DerivesFrom(type, attribute))
        {
            return;
        }

        // An inherited [AttributeUsage] already states the targets, so a type with one anywhere on its chain
        // is covered. The walk stops at Attribute itself: whatever the framework declares on the base class is
        // the default every attribute already has, not a decision this type made.
        for (var current = type; current is not null && !SymbolEqualityComparer.Default.Equals(current, attribute); current = current.BaseType)
        {
            var attributes = current.GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, usage))
                {
                    return;
                }
            }
        }

        var location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
        context.ReportDiagnostic(DiagnosticHelper.Create(DesignRules.MissingAttributeUsage, location, type.Name));
    }

    /// <summary>Returns whether a type derives from the attribute base type.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="attribute">The attribute base type.</param>
    /// <returns><see langword="true"/> when the type is an attribute.</returns>
    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol attribute)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, attribute))
            {
                return true;
            }
        }

        return false;
    }
}
