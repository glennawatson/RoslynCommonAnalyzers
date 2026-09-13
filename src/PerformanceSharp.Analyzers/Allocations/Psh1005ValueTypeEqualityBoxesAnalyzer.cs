// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports public or internal structs that define no equality members (PSH1005). A struct
/// that neither overrides <c>Equals(object)</c> nor implements <c>IEquatable&lt;T&gt;</c>
/// of itself falls back to the inherited <c>ValueType.Equals</c>, which boxes both
/// operands and may reflect over their fields on every comparison. Ref-like structs
/// (which cannot box), record structs (which synthesize equality members), and
/// compiler-generated or less-visible helper structs are not reported. The
/// <c>IEquatable`1</c> definition is resolved once per compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1005ValueTypeEqualityBoxesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AllocationRules.ValueTypeEqualityBoxes);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var equatableType = new EquatableType(start.Compilation);
            start.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, equatableType), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports PSH1005 for a boxing-prone struct that defines no equality members.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="equatableType">The deferred <c>IEquatable`1</c> definition.</param>
    private static void AnalyzeNamedType(in SymbolAnalysisContext context, EquatableType equatableType)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!IsCandidateStruct(type)
            || OverridesObjectEquals(type)
            || equatableType.Get() is not { } equatableDefinition
            || ImplementsSelfEquatable(type, equatableDefinition))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.ValueTypeEqualityBoxes,
            type.Locations[0],
            type.Name));
    }

    /// <summary>Returns whether the type has the shape and visibility of a reportable struct.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>Whether the type needs its equality members checked.</returns>
    private static bool IsCandidateStruct(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Struct
            && !type.IsRefLikeType
            && !type.IsRecord
            && !type.IsImplicitlyDeclared
            && type.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
            && !type.Locations.IsEmpty;

    /// <summary>Returns whether a struct overrides <c>Equals(object)</c>.</summary>
    /// <param name="type">The struct to inspect.</param>
    /// <returns><see langword="true"/> when an <c>Equals(object)</c> override is declared.</returns>
    private static bool OverridesObjectEquals(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers(WellKnownMemberNames.ObjectEquals))
        {
            if (member is IMethodSymbol { IsOverride: true, Parameters.Length: 1 } method
                && method.Parameters[0].Type.SpecialType == SpecialType.System_Object)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a struct implements <c>IEquatable&lt;T&gt;</c> constructed with itself.</summary>
    /// <param name="type">The struct to inspect.</param>
    /// <param name="equatableDefinition">The <c>IEquatable`1</c> definition.</param>
    /// <returns><see langword="true"/> when the struct is equatable to itself.</returns>
    private static bool ImplementsSelfEquatable(INamedTypeSymbol type, INamedTypeSymbol equatableDefinition)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface.ConstructedFrom, equatableDefinition)
                && SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the equatable definition only when a candidate struct needs it.</summary>
    /// <param name="compilation">The compilation whose framework types are resolved.</param>
    private sealed class EquatableType(Compilation compilation)
    {
        /// <summary>The metadata name of the generic equatable interface.</summary>
        private const string EquatableMetadataName = "System.IEquatable`1";

        /// <summary>The cached definition, including a missing-type result.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the definition, resolving it on first demand.</summary>
        /// <returns>The equatable definition, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(EquatableMetadataName)])[0];
    }
}
