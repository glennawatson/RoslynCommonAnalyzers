// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an abstract class that declares no abstract member and inherits none it leaves unimplemented
/// (SST1496). Such a type cannot be instantiated, yet asks nothing of the types that derive from it.
/// </summary>
/// <remarks>
/// <para>
/// The rule runs as a symbol action, so a <c>partial</c> class is judged once, from all of its parts at
/// once — a member declared in another file counts, and the type is never reported twice. It also means the
/// filter is three symbol flags before anything is enumerated: only an abstract, non-static class reaches
/// the member scan.
/// </para>
/// <para>
/// A type that inherits an abstract member without overriding it is genuinely abstract and is left alone.
/// Deciding that costs a walk of the base chain, so it runs last, only for the abstract classes that have a
/// base type other than <c>object</c> and declared nothing abstract themselves.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1496AbstractTypeWithoutAbstractMembersAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(MaintainabilityRules.AbstractTypeWithoutAbstractMembers);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    /// <summary>Reports an abstract class with nothing abstract about it.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <remarks>
    /// A static class is abstract and sealed in metadata, so it is filtered out explicitly. A record is
    /// excluded because the compiler writes its equality contract and an abstract record base is how a
    /// closed hierarchy is spelled.
    /// </remarks>
    private static void Analyze(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: true, IsStatic: false, IsRecord: false } type)
        {
            return;
        }

        if (DeclaresAbstractMember(type) || InheritsUnimplementedAbstractMember(type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.AbstractTypeWithoutAbstractMembers,
            type.Locations[0],
            type.Name));
    }

    /// <summary>Returns whether the type itself declares an abstract member.</summary>
    /// <param name="type">The abstract class.</param>
    /// <returns><see langword="true"/> when some member asks derived types to supply it.</returns>
    private static bool DeclaresAbstractMember(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i].IsAbstract)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the type inherits an abstract member that nothing in its chain overrides.</summary>
    /// <param name="type">The abstract class.</param>
    /// <returns><see langword="true"/> when the type is abstract because its base still is.</returns>
    private static bool InheritsUnimplementedAbstractMember(INamedTypeSymbol type)
    {
        if (type.BaseType is not { } baseType || baseType.SpecialType == SpecialType.System_Object)
        {
            return false;
        }

        var overridden = CollectOverriddenMembers(type);
        for (var current = baseType; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            var members = current.GetMembers();
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i].IsAbstract && !overridden.Contains(members[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Collects every base member overridden anywhere between the type and <c>object</c>.</summary>
    /// <param name="type">The abstract class.</param>
    /// <returns>The set of members an override in the chain already supplies.</returns>
    /// <remarks>
    /// An override two levels up still satisfies the abstract member it descends from, so each override is
    /// followed all the way to its root declaration rather than only one step.
    /// </remarks>
    private static HashSet<ISymbol> CollectOverriddenMembers(INamedTypeSymbol type)
    {
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            var members = current.GetMembers();
            for (var i = 0; i < members.Length; i++)
            {
                for (var next = GetOverriddenMember(members[i]); next is not null; next = GetOverriddenMember(next))
                {
                    if (!overridden.Add(next))
                    {
                        break;
                    }
                }
            }
        }

        return overridden;
    }

    /// <summary>Gets the member a declaration overrides, if it overrides one.</summary>
    /// <param name="member">The member.</param>
    /// <returns>The overridden member, or <see langword="null"/>.</returns>
    private static ISymbol? GetOverriddenMember(ISymbol member) => member switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null,
    };
}
