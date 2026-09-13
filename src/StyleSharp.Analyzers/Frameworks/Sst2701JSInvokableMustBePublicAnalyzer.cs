// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method annotated <c>[JSInvokable]</c> that is not <c>public</c> (SST2701). JavaScript interop
/// resolves such a method by name across the interop boundary and can only bind a public one, so a private,
/// internal, or protected method carries the attribute yet is silently uncallable at runtime.
/// </summary>
/// <remarks>
/// The <c>Microsoft.JSInterop.JSInvokableAttribute</c> marker is resolved only for a non-public ordinary
/// method with attributes. Analysis stays on the method symbol so partial declarations share their attributes
/// and diagnostic location. A method with no attributes performs no metadata lookup.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2701JSInvokableMustBePublicAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the JavaScript-interop invokable attribute.</summary>
    private const string JSInvokableAttributeMetadataName = "Microsoft.JSInterop.JSInvokableAttribute";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(FrameworksRules.JSInvokableMustBePublic);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSymbolAction(static symbolContext => AnalyzeMethod(symbolContext), SymbolKind.Method);
    }

    /// <summary>Reports a non-public method that carries the invokable attribute.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeMethod(in SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Ordinary || method.DeclaredAccessibility == Accessibility.Public)
        {
            return;
        }

        if (!HasMarker(method, context.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.JSInvokableMustBePublic, method.Locations[0], method.Name));
    }

    /// <summary>Returns whether a method carries the invokable marker attribute.</summary>
    /// <param name="method">The method to inspect.</param>
    /// <param name="compilation">The compilation used to resolve the marker after attributes are found.</param>
    /// <returns><see langword="true"/> when the marker is present.</returns>
    private static bool HasMarker(IMethodSymbol method, Compilation compilation)
    {
        var attributes = method.GetAttributes();
        if (attributes.IsEmpty
            || compilation.GetTypeByMetadataName(JSInvokableAttributeMetadataName) is not { } marker)
        {
            return false;
        }

        for (var i = 0; i < attributes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, marker))
            {
                return true;
            }
        }

        return false;
    }
}
