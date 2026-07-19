// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member whose declared accessibility is wider than the effective accessibility of its containing
/// type (SST2324). The container caps the member's real reach, so a modifier that promises more is dead and
/// misleading: a reader takes a <c>public</c> method on an <c>internal</c> class — or a <c>public</c> nested
/// type inside one — for part of the public surface when nothing outside the assembly can ever touch it.
/// </summary>
/// <remarks>
/// The containing type's effective accessibility is computed by walking every enclosing type: a <c>public</c>
/// member of a <c>public</c> type nested in an <c>internal</c> type is still effectively internal, so it is
/// reported. Accessibility is modelled as the set of caller categories it admits, so a member is reported only
/// when its caller set is a strict superset of the container's — <c>protected</c> and <c>internal</c> admit
/// disjoint sets and neither is treated as wider than the other.
/// <para>
/// The clean path is symbol-only and allocation-free: each type's members are scanned, and syntax is read only
/// once a widening member is found, to point the diagnostic at the offending modifier keyword. A member whose
/// accessibility is fixed by a contract it cannot narrow is left alone — an <c>override</c> matches its base,
/// an explicit or implicit interface implementation matches the interface — as is any member declared no wider
/// than its container, and every member of a <c>public</c> top-level type, which caps nothing.
/// </para>
/// <para>
/// Interface and enum members carry no author-written accessibility modifier to narrow and are never reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Caller category: a derived type in the same assembly.</summary>
    private const int SameAssemblyDerived = 0b0001;

    /// <summary>Caller category: a derived type in another assembly.</summary>
    private const int OtherAssemblyDerived = 0b0010;

    /// <summary>Caller category: a non-derived type in the same assembly.</summary>
    private const int SameAssemblyOther = 0b0100;

    /// <summary>Caller category: a non-derived type in another assembly.</summary>
    private const int OtherAssemblyOther = 0b1000;

    /// <summary>The caller set a <c>public</c> element admits — everything.</summary>
    private const int FullReach = SameAssemblyDerived | OtherAssemblyDerived | SameAssemblyOther | OtherAssemblyOther;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.MemberMoreAccessibleThanContainingType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Reports each member of a type whose modifier promises more reach than the type can deliver.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // An interface's members are implicitly public and an enum's are implicitly public: neither carries a
        // modifier the author could narrow, so a mismatch there is nothing this rule can ask them to fix.
        if (type.TypeKind is TypeKind.Interface or TypeKind.Enum)
        {
            return;
        }

        var containerReach = EffectiveReach(type);

        // A public type caps nothing — no member can be declared wider than the whole assembly already sees.
        if (containerReach == FullReach)
        {
            return;
        }

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (!IsCandidateMember(member))
            {
                continue;
            }

            var memberReach = AccessibilityReach(member.DeclaredAccessibility);
            if (!IsWider(memberReach, containerReach))
            {
                continue;
            }

            // From here the widening is real and rare. A member whose accessibility a contract fixes cannot be
            // narrowed, so it is not something to report.
            if (ImplementsInterfaceMember(type, member))
            {
                continue;
            }

            // Syntax is read only now, to point the diagnostic at the offending modifier keyword.
            if (AccessModifierLocation(member, context.CancellationToken) is not { } location)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DesignRules.MemberMoreAccessibleThanContainingType,
                location,
                member.Name,
                AccessibilityKeyword(member.DeclaredAccessibility),
                ReachKeyword(containerReach)));
        }
    }

    /// <summary>Returns whether a member is one whose author-written accessibility this rule can weigh.</summary>
    /// <param name="member">The declared member.</param>
    /// <returns><see langword="true"/> for a non-override, non-synthesized method, property, event, field, or nested type.</returns>
    private static bool IsCandidateMember(ISymbol member)
    {
        // An override matches its base member's accessibility and cannot be narrowed here.
        if (member.IsImplicitlyDeclared || member.IsOverride)
        {
            return false;
        }

        return member switch
        {
            IMethodSymbol method => method.MethodKind == MethodKind.Ordinary,
            IPropertySymbol or IEventSymbol or IFieldSymbol or INamedTypeSymbol => true,
            _ => false,
        };
    }

    /// <summary>Folds a type and every enclosing type into the caller set the innermost is actually reachable from.</summary>
    /// <param name="type">The type whose effective reach is wanted.</param>
    /// <returns>The intersection of the reach of the type and each of its containers.</returns>
    private static int EffectiveReach(INamedTypeSymbol type)
    {
        var reach = FullReach;
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            reach &= AccessibilityReach(current.DeclaredAccessibility);
        }

        return reach;
    }

    /// <summary>Returns whether the member admits a strict superset of the callers its container admits.</summary>
    /// <param name="memberReach">The member's caller set.</param>
    /// <param name="containerReach">The container's effective caller set.</param>
    /// <returns><see langword="true"/> when the member promises reach the container cannot deliver.</returns>
    private static bool IsWider(int memberReach, int containerReach)
        => memberReach != containerReach && (memberReach & containerReach) == containerReach;

    /// <summary>Returns the set of caller categories an accessibility admits, as a bit mask.</summary>
    /// <param name="accessibility">The accessibility to model.</param>
    /// <returns>A mask over {same-assembly derived, other-assembly derived, same-assembly other, other-assembly other}.</returns>
    private static int AccessibilityReach(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => FullReach,
        Accessibility.ProtectedOrInternal => SameAssemblyDerived | OtherAssemblyDerived | SameAssemblyOther,
        Accessibility.Internal => SameAssemblyDerived | SameAssemblyOther,
        Accessibility.Protected => SameAssemblyDerived | OtherAssemblyDerived,
        Accessibility.ProtectedAndInternal => SameAssemblyDerived,
        _ => 0,
    };

    /// <summary>Returns the C# keyword spelling of an accessibility for the diagnostic message.</summary>
    /// <param name="accessibility">The accessibility to spell.</param>
    /// <returns>The keyword text.</returns>
    private static string AccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Private => "private",
        _ => accessibility.ToString(),
    };

    /// <summary>Returns the C# keyword spelling of a caller-set mask for the diagnostic message.</summary>
    /// <param name="reach">The caller-set mask, always one produced by <see cref="AccessibilityReach"/> or their intersection.</param>
    /// <returns>The keyword text naming the container's effective accessibility.</returns>
    private static string ReachKeyword(int reach) => reach switch
    {
        FullReach => "public",
        SameAssemblyDerived | OtherAssemblyDerived | SameAssemblyOther => "protected internal",
        SameAssemblyDerived | SameAssemblyOther => "internal",
        SameAssemblyDerived | OtherAssemblyDerived => "protected",
        SameAssemblyDerived => "private protected",
        _ => "private",
    };

    /// <summary>Returns whether a member is the implementation of an interface member, so its accessibility is fixed.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="member">The declared member under test.</param>
    /// <returns><see langword="true"/> when the member implements an interface member, implicitly or explicitly.</returns>
    private static bool ImplementsInterfaceMember(INamedTypeSymbol type, ISymbol member)
    {
        // Only a method, property (including an indexer), or event can implement an interface member; a field or
        // a nested type never does, and asking would only waste the interface walk.
        if (member is not (IMethodSymbol or IPropertySymbol or IEventSymbol))
        {
            return false;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var interfaceMembers = interfaces[i].GetMembers();
            for (var j = 0; j < interfaceMembers.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(interfaceMembers[j]), member))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns the location of the member's access-modifier keyword, or <see langword="null"/> when it has none.</summary>
    /// <param name="member">The member whose modifier is wanted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The modifier keyword's location, or <see langword="null"/>.</returns>
    private static Location? AccessModifierLocation(ISymbol member, CancellationToken cancellationToken)
    {
        var references = member.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            var node = references[i].GetSyntax(cancellationToken);

            // A field or field-like event points at its declarator; the modifiers sit on the enclosing
            // field/event declaration two nodes up.
            if (node is VariableDeclaratorSyntax variable)
            {
                node = variable.Parent?.Parent;
            }

            if (node is not MemberDeclarationSyntax declaration)
            {
                continue;
            }

            var modifiers = declaration.Modifiers;
            for (var j = 0; j < modifiers.Count; j++)
            {
                if (modifiers[j].Kind() is SyntaxKind.PublicKeyword
                    or SyntaxKind.PrivateKeyword
                    or SyntaxKind.ProtectedKeyword
                    or SyntaxKind.InternalKeyword)
                {
                    return modifiers[j].GetLocation();
                }
            }
        }

        return null;
    }
}
