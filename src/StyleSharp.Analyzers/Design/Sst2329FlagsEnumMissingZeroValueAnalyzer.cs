// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>[Flags]</c> enum that declares no zero-valued member (SST2329). A flags enum is a set, and
/// the empty set needs a name — <c>None = 0</c> by convention — so that a default-initialized value, a
/// "nothing selected" comparison, and a <c>ToString</c> of zero all have something to say.
/// </summary>
/// <remarks>
/// <para>
/// This is the sibling of the distinct-bit-values rule, and the two do not overlap: that rule leaves a zero
/// member alone as the expected empty set and never requires one, while this rule is only about the empty
/// set's absence and says nothing about whether the other members own distinct bits.
/// </para>
/// <para>
/// The clean path is a type-kind check, so a compilation's non-enum types cost one comparison each, and an
/// enum without the attribute costs one attribute scan.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2329FlagsEnumMissingZeroValueAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DesignRules.FlagsEnumMissingZeroValue);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Reports a flags enum whose members never include the empty set.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Enum
            || !EnumFlagValues.HasFlagsAttribute(type)
            || type.Locations.Length == 0
            || !type.Locations[0].IsInSource)
        {
            return;
        }

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (EnumFlagValues.TryGetValue(members[i], out var value) && value == 0)
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(DesignRules.FlagsEnumMissingZeroValue, type.Locations[0], type.Name));
    }
}
