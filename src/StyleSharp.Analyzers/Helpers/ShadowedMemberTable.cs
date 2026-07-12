// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The fields and properties one type makes reachable by simple name — its own, plus the ones it inherits
/// and can still see — indexed by name so SST1484 can answer "does this declaration reuse a member's name?"
/// with a single hash probe.
/// </summary>
/// <remarks>
/// Built once per type declaration and cached on it. The name is the whole question: C# resolves a simple
/// name against the members of the containing type and its bases, and a local or parameter of the same name
/// wins that lookup, which is exactly the ambiguity the rule reports.
/// </remarks>
internal sealed class ShadowedMemberTable
{
    /// <summary>The visible members keyed by name, or <see langword="null"/> when there are none.</summary>
    private readonly Dictionary<string, ShadowedMember>? _members;

    /// <summary>Initializes a new instance of the <see cref="ShadowedMemberTable"/> class.</summary>
    /// <param name="members">The visible members keyed by name, or <see langword="null"/> when there are none.</param>
    private ShadowedMemberTable(Dictionary<string, ShadowedMember>? members) => _members = members;

    /// <summary>Gets the table used when nothing can be shadowed: a type that neither declares nor inherits anything shadowable.</summary>
    public static ShadowedMemberTable Empty { get; } = new(null);

    /// <summary>Builds the table of members a type can see by simple name.</summary>
    /// <param name="type">The type whose members and inherited members are indexed.</param>
    /// <returns>The table, or <see cref="Empty"/> when the type sees no field or property.</returns>
    /// <remarks>
    /// The walk runs from the type outwards, so the nearest declaration of a name wins — which is how C#
    /// resolves it. A private member of a base type is invisible here and is skipped, or a local would be
    /// reported for shadowing something it cannot even name.
    /// </remarks>
    public static ShadowedMemberTable Create(INamedTypeSymbol type)
    {
        var members = new Dictionary<string, ShadowedMember>(type.GetMembers().Length, StringComparer.Ordinal);
        var inherited = false;
        for (INamedTypeSymbol? current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            AddDeclaredMembers(members, current, type, inherited);
            inherited = true;
        }

        return members.Count == 0 ? Empty : new ShadowedMemberTable(members);
    }

    /// <summary>Looks up the member a name would resolve to.</summary>
    /// <param name="name">The declared name.</param>
    /// <param name="member">The member the name already denotes.</param>
    /// <returns><see langword="true"/> when the name is already taken by a visible field or property.</returns>
    public bool TryGet(string name, out ShadowedMember member)
    {
        if (_members is null)
        {
            member = default;
            return false;
        }

        return _members.TryGetValue(name, out member);
    }

    /// <summary>Indexes the fields and properties one type in the chain declares.</summary>
    /// <param name="members">The table being built.</param>
    /// <param name="declaring">The type whose declared members are being indexed.</param>
    /// <param name="type">The type the table is being built for.</param>
    /// <param name="inherited">Whether <paramref name="declaring"/> is a base type of <paramref name="type"/>.</param>
    private static void AddDeclaredMembers(
        Dictionary<string, ShadowedMember> members,
        INamedTypeSymbol declaring,
        INamedTypeSymbol type,
        bool inherited)
    {
        var declared = declaring.GetMembers();
        for (var i = 0; i < declared.Length; i++)
        {
            var symbol = declared[i];
            if (!IsShadowable(symbol, out var isProperty) || (inherited && !IsVisibleToDerivedType(symbol, type)))
            {
                continue;
            }

            Add(members, symbol, isProperty, inherited);
        }
    }

    /// <summary>Records one member, keeping the nearest declaration of a name.</summary>
    /// <param name="members">The table being built.</param>
    /// <param name="symbol">The member being recorded.</param>
    /// <param name="isProperty">Whether the member is a property.</param>
    /// <param name="inherited">Whether the member comes from a base type.</param>
    /// <remarks>
    /// When a nearer declaration already claimed the name there is nothing to add — a local shadows what the
    /// name resolves to, and that is the nearer one. The single fact worth keeping from the further
    /// declaration is that a field of the type hides an inherited field, which is what
    /// <c>check_base_types</c> reports.
    /// </remarks>
    private static void Add(Dictionary<string, ShadowedMember> members, ISymbol symbol, bool isProperty, bool inherited)
    {
        var name = symbol.Name;
        if (!members.TryGetValue(name, out var existing))
        {
            members.Add(name, new ShadowedMember(isProperty, symbol.IsStatic, inherited, HidesInheritedField: false));
            return;
        }

        if (!inherited || isProperty || existing.IsInherited || existing.IsProperty || existing.HidesInheritedField)
        {
            return;
        }

        members[name] = existing.HidingInheritedField();
    }

    /// <summary>Returns whether a member is one a declaration can shadow.</summary>
    /// <param name="symbol">The member.</param>
    /// <param name="isProperty">Whether the member is a property rather than a field.</param>
    /// <returns><see langword="true"/> for a field or a non-indexer property that source can name.</returns>
    /// <remarks>
    /// A compiler-generated member is skipped: a property's backing field and a primary constructor's capture
    /// field have no name a reader could have collided with, and reporting them would be reporting the
    /// compiler's own choices.
    /// </remarks>
    private static bool IsShadowable(ISymbol symbol, out bool isProperty)
    {
        isProperty = false;
        if (symbol.IsImplicitlyDeclared || !symbol.CanBeReferencedByName)
        {
            return false;
        }

        switch (symbol)
        {
            case IFieldSymbol:
                return true;
            case IPropertySymbol { IsIndexer: false }:
            {
                isProperty = true;
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Returns whether a base type's member is visible to the type that inherits it.</summary>
    /// <param name="member">The base type's member.</param>
    /// <param name="type">The type the table is being built for.</param>
    /// <returns><see langword="true"/> when the derived type can refer to the member by simple name.</returns>
    private static bool IsVisibleToDerivedType(ISymbol member, INamedTypeSymbol type) => member.DeclaredAccessibility switch
    {
        Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal => true,
        Accessibility.Internal or Accessibility.ProtectedAndInternal =>
            SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, type.ContainingAssembly),
        _ => false,
    };
}
