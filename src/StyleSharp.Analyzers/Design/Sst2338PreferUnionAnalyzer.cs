// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a type that pairs an enum discriminator with several mutually exclusive payload members —
/// a discriminated union written by hand, which C# 15 can state directly (SST2338).
/// </summary>
/// <remarks>
/// Gated on the union marker interface resolving in the compilation, so the rule costs nothing and
/// stays silent on a target framework whose runtime has no union support, and on C# 15 being available
/// so the suggested syntax would actually compile.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2338PreferUnionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the marker interface that identifies a union.</summary>
    private const string UnionMarkerMetadataName = "System.Runtime.CompilerServices.IUnion";

    /// <summary>How many distinct payload types make a tagged type a union rather than a plain record.</summary>
    /// <remarks>A single payload beside a tag is an optional value, not a choice between alternatives.</remarks>
    private const int MinimumPayloads = 2;

    /// <summary>The name endings that read as a discriminator rather than as data.</summary>
    private static readonly string[] DiscriminatorSuffixes = ["Kind", "Tag", "Case", "Discriminator", "Type"];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(DesignRules.PreferUnion);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var unionMarker = start.Compilation.GetTypeByMetadataName(UnionMarkerMetadataName);
            if (unionMarker is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, unionMarker), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports one type whose shape is a hand-rolled union.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="unionMarker">The resolved union marker symbol.</param>
    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol unionMarker)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!IsEligible(type, unionMarker) || !HasSingleDiscriminator(type))
        {
            return;
        }

        if (CountDistinctPayloads(type) < MinimumPayloads)
        {
            return;
        }

        if (type.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) is not TypeDeclarationSyntax declaration
            || !LanguageVersions.SupportsCSharp15(declaration))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.PreferUnion,
            declaration.Identifier.GetLocation(),
            type.Name));
    }

    /// <summary>Gets whether a type is the kind of declaration this rule considers at all.</summary>
    /// <param name="type">The declared type.</param>
    /// <param name="unionMarker">The resolved union marker symbol.</param>
    /// <returns><see langword="true"/> for a concrete class or struct that is not already a union.</returns>
    private static bool IsEligible(INamedTypeSymbol type, INamedTypeSymbol unionMarker)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || type.IsAbstract
            || type.IsStatic
            || type.DeclaringSyntaxReferences.Length == 0)
        {
            return false;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented, unionMarker))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets whether a type declares exactly one enum-typed member that reads as a discriminator.</summary>
    /// <param name="type">The declared type.</param>
    /// <returns><see langword="true"/> when there is exactly one.</returns>
    private static bool HasSingleDiscriminator(INamedTypeSymbol type)
    {
        var found = 0;
        foreach (var member in type.GetMembers())
        {
            var memberType = PayloadType(member);
            if (memberType is { TypeKind: TypeKind.Enum } && IsDiscriminatorName(member.Name))
            {
                found++;
            }
        }

        return found == 1;
    }

    /// <summary>Counts the distinct payload types held alongside the discriminator.</summary>
    /// <param name="type">The declared type.</param>
    /// <returns>The number of distinct nullable or reference-typed members.</returns>
    private static int CountDistinctPayloads(INamedTypeSymbol type)
    {
        const int InitialPayloadTypeCapacity = 4;

        var seen = new List<ITypeSymbol>(InitialPayloadTypeCapacity);
        foreach (var member in type.GetMembers())
        {
            var memberType = PayloadType(member);
            if (memberType is null || !IsPayload(memberType) || Contains(seen, memberType))
            {
                continue;
            }

            seen.Add(memberType);
        }

        return seen.Count;
    }

    /// <summary>Gets whether a list already holds a type.</summary>
    /// <param name="seen">The types collected so far.</param>
    /// <param name="candidate">The type to test.</param>
    /// <returns><see langword="true"/> when already present.</returns>
    private static bool Contains(List<ITypeSymbol> seen, ITypeSymbol candidate)
    {
        for (var i = 0; i < seen.Count; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(seen[i], candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the type of a member that could hold a payload, ignoring everything else.</summary>
    /// <param name="member">The type member.</param>
    /// <returns>The member's type, or <see langword="null"/> when it cannot hold state.</returns>
    private static ITypeSymbol? PayloadType(ISymbol member)
    {
        if (member.IsStatic || member.IsImplicitlyDeclared)
        {
            return null;
        }

        return member switch
        {
            IFieldSymbol { IsConst: false } field => field.Type,
            IPropertySymbol { IsIndexer: false } property => property.Type,
            _ => null,
        };
    }

    /// <summary>Gets whether a member type can be one arm of a discriminated value.</summary>
    /// <param name="type">The member type.</param>
    /// <returns><see langword="true"/> for a reference type or a nullable value type.</returns>
    private static bool IsPayload(ITypeSymbol type)
        => type.TypeKind != TypeKind.Enum
            && (type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

    /// <summary>Gets whether a member name reads as a discriminator.</summary>
    /// <param name="name">The member name.</param>
    /// <returns><see langword="true"/> when the name ends in a discriminator word.</returns>
    private static bool IsDiscriminatorName(string name)
    {
        foreach (var suffix in DiscriminatorSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
