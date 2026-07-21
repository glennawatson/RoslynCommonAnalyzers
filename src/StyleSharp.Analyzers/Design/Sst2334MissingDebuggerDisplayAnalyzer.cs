// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a publicly visible class, struct, or record that carries no <c>[DebuggerDisplay]</c> attribute
/// (SST2334). In the debugger such a type shows as its bare type name, so a collection of instances is opaque
/// until each is expanded; a display string surfaces the one or two members that identify an instance.
/// </summary>
/// <remarks>
/// This is an opinionated, heavy nudge that would otherwise fire on nearly every public type, so it is
/// disabled by default and opt-in through <c>.editorconfig</c>. The rule resolves the attribute type in the
/// compilation and stays silent when it is absent, so the fix it offers always has something to add. The clean
/// path is an attribute scan on a type that is already externally visible.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2334MissingDebuggerDisplayAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the debugger-display attribute the rule and its fix depend on.</summary>
    private const string DebuggerDisplayMetadataName = "System.Diagnostics.DebuggerDisplayAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DesignRules.MissingDebuggerDisplay);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(DebuggerDisplayMetadataName) is not { } attribute)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, attribute), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports a publicly visible type with no debugger-display attribute.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="attributeType">The resolved debugger-display attribute type.</param>
    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attributeType)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || type.IsStatic
            || !SymbolVisibility.IsExternallyVisible(type)
            || type.Locations.Length == 0
            || !type.Locations[0].IsInSource
            || HasDebuggerDisplay(type, attributeType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DesignRules.MissingDebuggerDisplay, type.Locations[0], type.Name));
    }

    /// <summary>Returns whether a type already carries the debugger-display attribute.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="attributeType">The debugger-display attribute type.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    private static bool HasDebuggerDisplay(INamedTypeSymbol type, INamedTypeSymbol attributeType)
    {
        var attributes = type.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }
}
