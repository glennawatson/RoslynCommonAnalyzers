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
/// The <c>Microsoft.AspNetCore.Components.InjectAttribute</c> and <c>CascadingParameterAttribute</c> markers are
/// resolved only after a candidate attribute is found. The clean path is symbol-only and short-circuits on the
/// cheap flags first — a property that has a setter, is an indexer, or is static is dismissed before its attributes
/// are examined — so only a setter-less instance property is bound to a marker.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2712SetterlessInjectedPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the service-injection attribute.</summary>
    private const string InjectAttributeMetadataName = "Microsoft.AspNetCore.Components.InjectAttribute";

    /// <summary>The metadata name of the cascading-parameter attribute.</summary>
    private const string CascadingParameterAttributeMetadataName = "Microsoft.AspNetCore.Components.CascadingParameterAttribute";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(FrameworksRules.SetterlessInjectedProperty);

    /// <summary>The metadata names BindingMarkers resolves, in slot order.</summary>
    private static readonly string[] BindingMarkersMetadataNames =
    [
        InjectAttributeMetadataName,
        CascadingParameterAttributeMetadataName
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSymbolAction(
            context,
            static compilation => new LazyMetadataTypes(compilation, BindingMarkersMetadataNames),
            AnalyzeProperty,
            SymbolKind.Property);
    }

    /// <summary>Reports a setter-less property that carries an injection or cascading marker.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="markers">The injection and cascading markers resolved on demand.</param>
    private static void AnalyzeProperty(in SymbolAnalysisContext context, LazyMetadataTypes markers)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.SetMethod is not null || property.IsIndexer || property.IsStatic)
        {
            return;
        }

        if (!HasBindingMarker(property, markers))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.SetterlessInjectedProperty, property.Locations[0], property.Name));
    }

    /// <summary>Returns whether a property carries the injection or cascading marker attribute.</summary>
    /// <param name="property">The property to inspect.</param>
    /// <param name="markers">The injection and cascading markers resolved on demand.</param>
    /// <returns><see langword="true"/> when a marker is present.</returns>
    private static bool HasBindingMarker(IPropertySymbol property, LazyMetadataTypes markers)
    {
        var attributes = property.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var attributeClass = attributes[i].AttributeClass;
            if (attributeClass?.Name is not ("InjectAttribute" or "CascadingParameterAttribute"))
            {
                continue;
            }

            var types = markers.Get();
            var inject = types[0];
            var cascading = types[1];
            if ((inject is not null && SymbolEqualityComparer.Default.Equals(attributeClass, inject))
                || (cascading is not null && SymbolEqualityComparer.Default.Equals(attributeClass, cascading)))
            {
                return true;
            }
        }

        return false;
    }
}
