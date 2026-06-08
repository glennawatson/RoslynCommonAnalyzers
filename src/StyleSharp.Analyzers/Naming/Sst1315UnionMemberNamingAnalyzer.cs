// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires union types and their cases to follow the configured casing
/// convention (SST1315), defaulting to PascalCase. Configure with
/// <c>stylesharp.union_member_naming</c> in <c>.editorconfig</c>.
/// </summary>
/// <remarks>
/// C# 15 union syntax is not yet exposed by Roslyn, so — like SourceDocParserLib —
/// unions are detected structurally by the <c>System.Runtime.CompilerServices.IUnion</c>
/// marker interface rather than a version-specific API. The whole rule is gated on
/// that marker being present in the compilation, so it costs nothing when no unions
/// are in play and lights up automatically once they are.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1315UnionMemberNamingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the marker interface that identifies a union.</summary>
    private const string UnionMarkerMetadataName = "System.Runtime.CompilerServices.IUnion";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.UnionMember);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var unionMarker = start.Compilation.GetTypeByMetadataName(UnionMarkerMetadataName);
            if (unionMarker is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, unionMarker), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports a union type or case whose name does not match the configured convention.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="unionMarker">The resolved <c>IUnion</c> marker symbol.</param>
    private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol unionMarker)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!IsUnionRelated(type, unionMarker))
        {
            return;
        }

        var name = type.Name;
        if (name.Length == 0 || NamingHelper.IsAllUnderscores(name))
        {
            return;
        }

        if (type.Locations.Length == 0 || type.Locations[0].SourceTree is not { } tree)
        {
            return;
        }

        var location = type.Locations[0];
        var convention = NamingConventions.Read(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree),
            NamingConventions.UnionMemberSpecificKey,
            NamingConventions.UnionMemberGeneralKey,
            NamingConvention.PascalCase);

        if (NamingConventions.Conforms(name, convention))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(NamingDiagnostic.NewNameKey, NamingConventions.Suggest(name, convention));
        context.ReportDiagnostic(Diagnostic.Create(NamingRules.UnionMember, location, properties, name));
    }

    /// <summary>Returns whether <paramref name="type"/> is a union (implements the marker) or a union case (its base does).</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="unionMarker">The <c>IUnion</c> marker symbol.</param>
    /// <returns><see langword="true"/> when the type participates in a union.</returns>
    private static bool IsUnionRelated(INamedTypeSymbol type, INamedTypeSymbol unionMarker)
        => Implements(type, unionMarker) || (type.BaseType is { } baseType && Implements(baseType, unionMarker));

    /// <summary>Returns whether <paramref name="type"/> implements the <paramref name="marker"/> interface.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="marker">The marker interface.</param>
    /// <returns><see langword="true"/> when implemented.</returns>
    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol marker)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, marker))
            {
                return true;
            }
        }

        return false;
    }
}
