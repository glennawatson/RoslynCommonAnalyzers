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
/// The whole rule is gated at compilation start on the <c>Microsoft.JSInterop.JSInvokableAttribute</c> marker
/// resolving; a project that references no JavaScript-interop assembly registers nothing and pays nothing. The
/// clean path is symbol-only: each ordinary method's attribute list is scanned, and a method is bound to the
/// marker and its accessibility read only once an attribute is present, so a method with no attributes costs a
/// single empty-list check.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2701JSInvokableMustBePublicAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the JavaScript-interop invokable attribute.</summary>
    private const string JSInvokableAttributeMetadataName = "Microsoft.JSInterop.JSInvokableAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.JSInvokableMustBePublic);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var marker = start.Compilation.GetTypeByMetadataName(JSInvokableAttributeMetadataName);
            if (marker is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, marker), SymbolKind.Method);
        });
    }

    /// <summary>Reports a non-public method that carries the invokable attribute.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="marker">The resolved invokable attribute type.</param>
    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol marker)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Ordinary || method.DeclaredAccessibility == Accessibility.Public)
        {
            return;
        }

        if (!HasMarker(method, marker))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.JSInvokableMustBePublic, method.Locations[0], method.Name));
    }

    /// <summary>Returns whether a method carries the invokable marker attribute.</summary>
    /// <param name="method">The method to inspect.</param>
    /// <param name="marker">The resolved invokable attribute type.</param>
    /// <returns><see langword="true"/> when the marker is present.</returns>
    private static bool HasMarker(IMethodSymbol method, INamedTypeSymbol marker)
    {
        var attributes = method.GetAttributes();
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
