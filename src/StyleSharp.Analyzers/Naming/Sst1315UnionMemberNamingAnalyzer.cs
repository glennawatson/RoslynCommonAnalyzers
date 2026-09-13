// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires union types and their cases to follow the configured casing
/// convention (SST1315), defaulting to PascalCase. Configure with
/// <c>stylesharp.union_member_naming</c> in <c>.editorconfig</c>.
/// </summary>
/// <remarks>
/// Unions are detected structurally, by the <c>System.Runtime.CompilerServices.IUnion</c>
/// marker interface, rather than through the C# 15 <c>UnionDeclarationSyntax</c> that only
/// the roslyn5.9 slot can bind. Naming is a question about the symbol, not the syntax, so
/// the marker answers it on every slot and catches a union declared in a referenced
/// assembly as well as one declared here. The marker is resolved on first demand for a
/// type with a possible union interface whose name needs changing, and cached for the compilation.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1315UnionMemberNamingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(NamingRules.UnionMember);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var unionTypes = new UnionTypes(start.Compilation);
            start.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, unionTypes), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports a union type or case whose name does not match the configured convention.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="unionTypes">The union marker type cache for this compilation.</param>
    private static void AnalyzeType(in SymbolAnalysisContext context, UnionTypes unionTypes)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!MayBeUnionRelated(type))
        {
            return;
        }

        var name = type.Name;
        if (name.Length == 0 || NamingHelper.IsAllUnderscores(name))
        {
            return;
        }

        if (type.Locations.IsEmpty || type.Locations[0].SourceTree is not { } tree)
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

        if (unionTypes.Get() is not { } unionMarker || !IsUnionRelated(type, unionMarker))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(NamingDiagnostic.NewNameKey, NamingConventions.Suggest(name, convention));
        context.ReportDiagnostic(Diagnostic.Create(NamingRules.UnionMember, location, properties, name));
    }

    /// <summary>Checks whether a type or its base could implement the union marker before resolving it.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>True when the type or its base has a possible union interface.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MayBeUnionRelated(INamedTypeSymbol type) =>
        HasUnionInterfaceName(type) || (type.BaseType is { } baseType && HasUnionInterfaceName(baseType));

    /// <summary>Checks interface names before resolving the union marker from metadata.</summary>
    /// <param name="type">The type whose interfaces are inspected.</param>
    /// <returns>True when an implemented interface could be the union marker.</returns>
    private static bool HasUnionInterfaceName(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name == "IUnion")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether <paramref name="type"/> is a union (implements the marker) or a union case (its base does).</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="unionMarker">The <c>IUnion</c> marker symbol.</param>
    /// <returns><see langword="true"/> when the type participates in a union.</returns>
    private static bool IsUnionRelated(INamedTypeSymbol type, INamedTypeSymbol unionMarker) =>
        Implements(type, unionMarker) || (type.BaseType is { } baseType && Implements(baseType, unionMarker));

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

    /// <summary>Resolves the union marker on first demand within one compilation.</summary>
    /// <param name="compilation">The compilation whose references are searched.</param>
    private sealed class UnionTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the marker interface that identifies a union.</summary>
        private const string UnionMarkerMetadataName = "System.Runtime.CompilerServices.IUnion";

        /// <summary>Stores the resolved symbol, including a missing result, in an atomically assigned array.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the union marker, resolving it on first demand.</summary>
        /// <returns>The marker type, or null when it is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(UnionMarkerMetadataName)])[0];
    }
}
