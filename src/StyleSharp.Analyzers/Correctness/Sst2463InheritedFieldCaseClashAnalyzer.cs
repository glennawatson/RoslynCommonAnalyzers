// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a derived type's instance field whose name matches an accessible field it inherits from a base type
/// when the two names are compared case-insensitively but are not identical (SST2463) — for example base
/// <c>protected int _value;</c> and derived <c>int _Value;</c>. The compiler keeps the two as distinct storage,
/// yet an unqualified reference to either name compiles inside the derived type, so getting the case wrong
/// silently reads or writes the other field.
/// </summary>
/// <remarks>
/// The clean path is cheap: a type that is not a class, or whose base is <c>object</c>, is rejected before any
/// member is touched, and the base chain is climbed only for a type that declares at least one own instance
/// field. A base field is a candidate only when it is a non-private instance field, so a private field the
/// derived type cannot see is never matched. An exactly matching name is deliberate hiding and is left to that
/// concern; only an ordinal-ignore-case match that is not an ordinal-equal match is reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2463InheritedFieldCaseClashAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.InheritedFieldCaseClash);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(static symbolContext => ClassInheritance.AnalyzeOwnMembers(symbolContext, ReportIfInheritedCaseClash), SymbolKind.NamedType);
    }

    /// <summary>
    /// Climbs the base chain for a non-private instance field whose name differs only by case from
    /// <paramref name="member"/>, when that member is one of the derived class's own instance fields.
    /// </summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="member">The member the class declares.</param>
    private static void ReportIfInheritedCaseClash(in SymbolAnalysisContext context, ISymbol member)
    {
        if (member is not IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field)
        {
            return;
        }

        var name = field.Name;
        for (var baseType = field.ContainingType.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            var baseMembers = baseType.GetMembers();
            for (var i = 0; i < baseMembers.Length; i++)
            {
                if (baseMembers[i] is not IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } baseField
                    || baseField.DeclaredAccessibility == Accessibility.Private)
                {
                    continue;
                }

                var baseName = baseField.Name;
                if (string.Equals(name, baseName, StringComparison.Ordinal) || !string.Equals(name, baseName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                context.ReportDiagnostic(DiagnosticHelper.Create(
                    CorrectnessRules.InheritedFieldCaseClash,
                    field.Locations[0],
                    name,
                    baseName,
                    baseField.ContainingType.Name));
                return;
            }
        }
    }
}
