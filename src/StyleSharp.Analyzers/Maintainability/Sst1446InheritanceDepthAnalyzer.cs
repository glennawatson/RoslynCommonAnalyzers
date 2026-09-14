// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags classes that sit deeper in an inheritance chain than the configured maximum (SST1446).
/// Depth counts the ancestors declared in the analyzed assembly, so deriving from a framework
/// base like <c>Exception</c> costs nothing; set
/// <c>stylesharp.SST1446.count_external_types = true</c> to count every ancestor below
/// <c>object</c> instead. The maximum defaults to 5 and is configured with
/// <c>stylesharp.SST1446.max_inheritance_depth</c>. The walk is a pointer chase over base types
/// and reads options only once a chain is at least two levels deep.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1446InheritanceDepthAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The default maximum inheritance depth.</summary>
    private const int DefaultMaximumDepth = 5;

    /// <summary>The smallest depth that can ever be reported (a threshold of 1 flags depth 2).</summary>
    private const int MinimumReportableDepth = 2;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.InheritanceDepth);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Measures a class's inheritance depth and reports it when over the maximum.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsStatic)
        {
            return;
        }

        // Count every ancestor below object first — a cheap pointer walk — and only consult
        // configuration when the chain could plausibly be reported.
        var totalDepth = 0;
        var ownedDepth = 0;
        for (var current = type.BaseType; current is { SpecialType: not SpecialType.System_Object }; current = current.BaseType)
        {
            totalDepth++;
            if (SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, context.Compilation.Assembly))
            {
                ownedDepth++;
            }
        }

        if (totalDepth < MinimumReportableDepth)
        {
            return;
        }

        if (type.Locations.IsEmpty || type.Locations[0].SourceTree is not { } tree)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        var depth = AnalyzerOptionReader.ReadBool(options, "stylesharp.SST1446.count_external_types", "stylesharp.count_external_types") ? totalDepth : ownedDepth;
        var maximum = AnalyzerOptionReader.ReadPositiveInt(options, "stylesharp.SST1446.max_inheritance_depth", "stylesharp.max_inheritance_depth", DefaultMaximumDepth);
        if (depth <= maximum)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MaintainabilityRules.InheritanceDepth,
            type.Locations[0],
            type.Name,
            depth,
            maximum));
    }
}
