// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a property marked <c>[Inject]</c> or <c>[CascadingParameter]</c> that has no setter (SST2712). The
/// runtime fills such a property by assigning it through reflection over the component's settable properties, so
/// a get-only or expression-bodied property is never assigned, stays null, and throws a
/// <c>NullReferenceException</c> at first use.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on the <c>Microsoft.AspNetCore.Components.InjectAttribute</c> or
/// <c>CascadingParameterAttribute</c> marker resolving; a project that references neither registers nothing and
/// pays nothing. The clean path is symbol-only and short-circuits on the cheap flags first — a property that has
/// a setter, is an indexer, or is static is dismissed before its attributes are examined — so only a setter-less
/// instance property is bound to a marker.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2712SetterlessInjectedPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the service-injection attribute.</summary>
    private const string InjectAttributeMetadataName = "Microsoft.AspNetCore.Components.InjectAttribute";

    /// <summary>The metadata name of the cascading-parameter attribute.</summary>
    private const string CascadingParameterAttributeMetadataName = "Microsoft.AspNetCore.Components.CascadingParameterAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.SetterlessInjectedProperty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            var inject = compilation.GetTypeByMetadataName(InjectAttributeMetadataName);
            var cascading = compilation.GetTypeByMetadataName(CascadingParameterAttributeMetadataName);
            if (inject is null && cascading is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => AnalyzeProperty(symbolContext, inject, cascading), SymbolKind.Property);
        });
    }

    /// <summary>Reports a setter-less property that carries an injection or cascading marker.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="inject">The resolved injection attribute type, if present.</param>
    /// <param name="cascading">The resolved cascading-parameter attribute type, if present.</param>
    private static void AnalyzeProperty(SymbolAnalysisContext context, INamedTypeSymbol? inject, INamedTypeSymbol? cascading)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.SetMethod is not null || property.IsIndexer || property.IsStatic)
        {
            return;
        }

        if (!HasBindingMarker(property, inject, cascading))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.SetterlessInjectedProperty, property.Locations[0], property.Name));
    }

    /// <summary>Returns whether a property carries the injection or cascading marker attribute.</summary>
    /// <param name="property">The property to inspect.</param>
    /// <param name="inject">The resolved injection attribute type, if present.</param>
    /// <param name="cascading">The resolved cascading-parameter attribute type, if present.</param>
    /// <returns><see langword="true"/> when a marker is present.</returns>
    private static bool HasBindingMarker(IPropertySymbol property, INamedTypeSymbol? inject, INamedTypeSymbol? cascading)
    {
        var attributes = property.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var attributeClass = attributes[i].AttributeClass;
            if ((inject is not null && SymbolEqualityComparer.Default.Equals(attributeClass, inject))
                || (cascading is not null && SymbolEqualityComparer.Default.Equals(attributeClass, cascading)))
            {
                return true;
            }
        }

        return false;
    }
}
