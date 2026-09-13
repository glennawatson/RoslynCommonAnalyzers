// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a concrete attribute type that declares no <c>[AttributeUsage]</c> (SST2336), so it silently
/// accepts every target, cannot repeat, and is not inherited.
/// </summary>
/// <remarks>
/// <c>Attribute</c> and <c>AttributeUsageAttribute</c> are resolved on first demand and cached per
/// compilation. Classes with no written base list never require those lookups.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2336MissingAttributeUsageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DesignRules.MissingAttributeUsage);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var types = new AttributeTypes(start.Compilation);
            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, types), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports one attribute type with no declared usage.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="types">The attribute types resolved on first demand.</param>
    private static void Analyze(in SymbolAnalysisContext context, AttributeTypes types)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } type
            || !HasBaseList(type, context.CancellationToken))
        {
            return;
        }

        var resolved = types.Get();
        if (resolved[0] is not { } attribute || resolved[1] is not { } usage
            || !DerivesFrom(type, attribute) || HasUsage(type, attribute, usage))
        {
            return;
        }

        var location = !type.Locations.IsEmpty ? type.Locations[0] : Location.None;
        context.ReportDiagnostic(DiagnosticHelper.Create(DesignRules.MissingAttributeUsage, location, type.Name));
    }

    /// <summary>Checks every partial declaration for a written base list before binding any base type.</summary>
    /// <param name="type">The candidate class.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a base list exists or the declaration shape is unknown.</returns>
    private static bool HasBaseList(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var references = type.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax(cancellationToken) is not TypeDeclarationSyntax declaration
                || declaration.BaseList is not null)
            {
                return true;
            }
        }

        return references.IsEmpty;
    }

    /// <summary>Checks for usage declared on the candidate or an intermediate attribute base.</summary>
    /// <param name="type">The candidate attribute type.</param>
    /// <param name="attribute">The framework attribute base type.</param>
    /// <param name="usage">The framework usage attribute type.</param>
    /// <returns><see langword="true"/> when usage is already specified.</returns>
    private static bool HasUsage(INamedTypeSymbol type, INamedTypeSymbol attribute, INamedTypeSymbol usage)
    {
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
                    return true;
                }
            }
        }

        return false;
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

    /// <summary>Resolves attribute types once per compilation, only for a candidate class.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    private sealed class AttributeTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the attribute base type.</summary>
        private const string AttributeMetadataName = "System.Attribute";

        /// <summary>The metadata name of the usage attribute.</summary>
        private const string AttributeUsageMetadataName = "System.AttributeUsageAttribute";

        /// <summary>The cached attribute and usage definitions, including missing types.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the attribute types, resolving them on first use.</summary>
        /// <returns>The attribute and usage type definitions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol?[] Get() => _resolved ??=
        [
            compilation.GetTypeByMetadataName(AttributeMetadataName),
            compilation.GetTypeByMetadataName(AttributeUsageMetadataName),
        ];
    }
}
