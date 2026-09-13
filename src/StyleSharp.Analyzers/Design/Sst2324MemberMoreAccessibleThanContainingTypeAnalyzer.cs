// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
    /// <summary>The diagnostic property carrying the accessibility the member should be declared with.</summary>
    internal const string TargetAccessibilityKey = "TargetAccessibility";

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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DesignRules.MemberMoreAccessibleThanContainingType);

    /// <summary>The property bag for each accessibility the fix can narrow to, built once rather than per report.</summary>
    private static readonly ImmutableDictionary<string, string?> ProtectedInternalProperties = TargetProperties("protected internal");

    /// <summary>The property bag naming <c>internal</c> as the target.</summary>
    private static readonly ImmutableDictionary<string, string?> InternalProperties = TargetProperties("internal");

    /// <summary>The property bag naming <c>protected</c> as the target.</summary>
    private static readonly ImmutableDictionary<string, string?> ProtectedProperties = TargetProperties("protected");

    /// <summary>The property bag naming <c>private protected</c> as the target.</summary>
    private static readonly ImmutableDictionary<string, string?> PrivateProtectedProperties = TargetProperties("private protected");

    /// <summary>The property bag naming <c>private</c> as the target.</summary>
    private static readonly ImmutableDictionary<string, string?> PrivateProperties = TargetProperties("private");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var publicMandatingAttributes = new PublicMandatingAttributes(start.Compilation);

            // A member inherited from a base class can implicitly implement an interface listed only by a
            // derived type, which forces it to stay public — a relationship invisible from the base's own
            // declaration. That view needs the whole source assembly, so it is built once and lazily: a
            // project with no reportable member (the common case) never pays for it.
            var inheritedInterfaceImplementations = new Lazy<HashSet<ISymbol>>(
                () => BuildInheritedInterfaceImplementationSet(start.Compilation),
                isThreadSafe: true);

            start.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, publicMandatingAttributes, inheritedInterfaceImplementations),
                SymbolKind.NamedType);
        });
    }

    /// <summary>Reports each member of a type whose modifier promises more reach than the type can deliver.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="publicMandatingAttributes">The deferred attributes whose framework requires the member be public.</param>
    /// <param name="inheritedInterfaceImplementations">The lazily-built set of inherited members that implicitly implement an interface.</param>
    private static void AnalyzeNamedType(in SymbolAnalysisContext context, PublicMandatingAttributes publicMandatingAttributes, Lazy<HashSet<ISymbol>> inheritedInterfaceImplementations)
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
            if (CarriesDataToReflection(member)
                || WideningModifierLocation(member, type, containerReach, publicMandatingAttributes, inheritedInterfaceImplementations, context.CancellationToken) is not { } location)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DesignRules.MemberMoreAccessibleThanContainingType,
                location,
                ReachProperties(containerReach),
                member.Name,
                AccessibilityKeyword(member.DeclaredAccessibility),
                ReachKeyword(containerReach)));
        }
    }

    /// <summary>Returns whether a member is one that reflection reads by accessibility.</summary>
    /// <param name="member">The declared member.</param>
    /// <returns><see langword="true"/> for a property or a field.</returns>
    /// <remarks>
    /// A member's accessibility is visible to reflection whatever its containing type's is, and the
    /// default contract of every reflection-based consumer — <c>System.Text.Json</c>, Newtonsoft,
    /// model binding, mapping — selects data members by exactly that. Narrowing a public property on
    /// an internal wrapper therefore does not remove unreachable surface; it removes the member from
    /// every payload, silently, while still compiling. A method is safe: nothing serializes one.
    /// </remarks>
    private static bool CarriesDataToReflection(ISymbol member) =>
        member is IPropertySymbol or IFieldSymbol;

    /// <summary>Returns the modifier location to report for a member wider than its container, or null to leave it alone.</summary>
    /// <param name="member">The declared member.</param>
    /// <param name="type">The containing type.</param>
    /// <param name="containerReach">The container's effective caller set.</param>
    /// <param name="publicMandatingAttributes">The deferred attributes whose framework requires the member be public.</param>
    /// <param name="inheritedInterfaceImplementations">The lazily-built set of inherited members that implicitly implement an interface.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The offending modifier's location, or <see langword="null"/> when nothing should be reported.</returns>
    private static Location? WideningModifierLocation(
        ISymbol member,
        INamedTypeSymbol type,
        int containerReach,
        PublicMandatingAttributes publicMandatingAttributes,
        Lazy<HashSet<ISymbol>> inheritedInterfaceImplementations,
        CancellationToken cancellationToken)
    {
        if (!IsCandidateMember(member) || !IsWider(AccessibilityReach(member.DeclaredAccessibility), containerReach))
        {
            return null;
        }

        // A member whose accessibility a contract fixes cannot be narrowed, so it is not something to report.
        if (ImplementsInterfaceMember(type, member))
        {
            return null;
        }

        // A member a framework requires to be public — a TUnit [Before]/[After] hook or a Blazor [Parameter] it
        // would otherwise reject — cannot be narrowed to match its container either, so it is left alone.
        if (HasPublicMandatingAttribute(member, publicMandatingAttributes))
        {
            return null;
        }

        // A member inherited by a derived type to implicitly implement an interface must stay public even though
        // its own declaring type lists no interface: narrowing it would fail with CS0737. This lookup builds the
        // whole-assembly view on first use, so only a project that has a reportable member ever pays for it.
        if (inheritedInterfaceImplementations.Value.Contains(member.OriginalDefinition))
        {
            return null;
        }

        // A member of a nested type whose reach excludes same-assembly non-derived callers cannot be narrowed to
        // match: private, private protected, and protected — the only accessibilities the rule could name here —
        // are all unreachable from the enclosing type (CS0122), so 'internal' is the minimum that compiles and
        // demanding less is unsatisfiable. Report such a member only when nothing outside its declaring type
        // names it, where making it private would actually compile.
        return (containerReach & SameAssemblyOther) == 0 && IsReferencedOutsideDeclaringType(member, cancellationToken) ? null : AccessModifierLocation(member, cancellationToken);
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
    private static bool IsWider(int memberReach, int containerReach) =>
        memberReach != containerReach && (memberReach & containerReach) == containerReach;

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

    /// <summary>Builds the property bag naming one target accessibility.</summary>
    /// <param name="keyword">The keyword text the fix should write.</param>
    /// <returns>The property bag.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ImmutableDictionary<string, string?> TargetProperties(string keyword) =>
        ImmutableDictionary<string, string?>.Empty.Add(TargetAccessibilityKey, keyword);

    /// <summary>Gets the cached property bag for a caller-set mask.</summary>
    /// <param name="reach">The container's effective caller set, which is never the full reach here.</param>
    /// <returns>The property bag naming the accessibility the member should carry.</returns>
    private static ImmutableDictionary<string, string?> ReachProperties(int reach) => reach switch
    {
        SameAssemblyDerived | OtherAssemblyDerived | SameAssemblyOther => ProtectedInternalProperties,
        SameAssemblyDerived | SameAssemblyOther => InternalProperties,
        SameAssemblyDerived | OtherAssemblyDerived => ProtectedProperties,
        SameAssemblyDerived => PrivateProtectedProperties,
        _ => PrivateProperties,
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

    /// <summary>Returns whether a member carries an attribute whose framework requires it be declared public.</summary>
    /// <param name="member">The declared member.</param>
    /// <param name="publicMandatingAttributes">The deferred public-mandating attribute types.</param>
    /// <returns><see langword="true"/> when the member carries one of the resolved attributes.</returns>
    private static bool HasPublicMandatingAttribute(ISymbol member, PublicMandatingAttributes publicMandatingAttributes)
    {
        var attributes = member.GetAttributes();
        if (attributes.IsEmpty)
        {
            return false;
        }

        var resolved = publicMandatingAttributes.Get();
        for (var i = 0; i < attributes.Length; i++)
        {
            var attributeClass = attributes[i].AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            for (var j = 0; j < resolved.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(attributeClass, resolved[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Builds the set of source members that a derived type uses to implicitly implement an interface member.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns>
    /// The inherited implementation members, by original definition. Only same-assembly types are walked (a member
    /// of an <c>internal</c> base is invisible across assemblies), and only implementations declared on a type
    /// other than the one implementing the interface — the inherited case the declaring type cannot see — are kept.
    /// </returns>
    private static HashSet<ISymbol> BuildInheritedInterfaceImplementationSet(Compilation compilation)
    {
        var result = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        INamespaceOrTypeSymbol globalNamespace = compilation.Assembly.GlobalNamespace;
        var pending = new Stack<INamespaceOrTypeSymbol>(Math.Max(1, globalNamespace.GetMembers().Length));
        pending.Push(globalNamespace);
        while (pending.Count > 0)
        {
            var members = pending.Pop().GetMembers();
            for (var i = 0; i < members.Length; i++)
            {
                switch (members[i])
                {
                    case INamespaceSymbol childNamespace:
                    {
                        pending.Push(childNamespace);
                        break;
                    }

                    case INamedTypeSymbol type:
                    {
                        pending.Push(type);
                        RecordInheritedInterfaceImplementations(type, result);
                        break;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Records each interface member of a type that is implemented by a member inherited from a base type.</summary>
    /// <param name="type">The type whose interface implementations are examined.</param>
    /// <param name="result">The set collecting inherited implementation members by original definition.</param>
    private static void RecordInheritedInterfaceImplementations(INamedTypeSymbol type, HashSet<ISymbol> result)
    {
        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var interfaceMembers = interfaces[i].GetMembers();
            for (var j = 0; j < interfaceMembers.Length; j++)
            {
                if (type.FindImplementationForInterfaceMember(interfaceMembers[j]) is { } implementation
                    && !SymbolEqualityComparer.Default.Equals(implementation.ContainingType, type))
                {
                    _ = result.Add(implementation.OriginalDefinition);
                }
            }
        }
    }

    /// <summary>Returns whether a member of a nested type is named anywhere outside that type's own declaration.</summary>
    /// <param name="member">The member declared in a nested type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the member's name appears in the enclosing type outside the declaring type.</returns>
    /// <remarks>
    /// A member of a nested type is only visible within the immediately enclosing type, so any use outside the
    /// declaring type appears in that enclosing type's declarations. The match is syntactic — a name occurrence,
    /// not a bound reference, since an analyzer must not build a semantic model here — so it errs toward leaving
    /// a member alone: an unrelated same-named identifier suppresses the report but never invents one. The
    /// declaring type's own subtree is excluded, so a self-reference does not count as an outside use.
    /// </remarks>
    private static bool IsReferencedOutsideDeclaringType(ISymbol member, CancellationToken cancellationToken)
    {
        var declaringType = member.ContainingType;
        if (declaringType?.ContainingType is not { } enclosing)
        {
            return false;
        }

        var state = new OutsideReferenceState(member.Name, DeclaringNodes(declaringType, cancellationToken));
        var enclosingReferences = enclosing.DeclaringSyntaxReferences;
        for (var i = 0; i < enclosingReferences.Length; i++)
        {
            var enclosingNode = enclosingReferences[i].GetSyntax(cancellationToken);
            if (!DescendantTraversalHelper.VisitDescendants(
                enclosingNode,
                ref state,
                static (SimpleNameSyntax name, ref OutsideReferenceState scan) =>
                {
                    if (name.Identifier.ValueText != scan.Name)
                    {
                        return true;
                    }

                    return IsInsideDeclaringType(name, scan.ExcludedDeclarations);
                }))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node is inside one of the declaring type's own declarations.</summary>
    /// <param name="node">The node whose ancestors are inspected.</param>
    /// <param name="declarations">The declarations excluded from outside-reference scanning.</param>
    /// <returns><see langword="true"/> when an ancestor is one of the excluded declarations.</returns>
    private static bool IsInsideDeclaringType(SyntaxNode node, SyntaxNode[] declarations)
    {
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (IsAny(ancestor, declarations))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Materializes a type's declaration nodes so they can be compared by reference during a walk.</summary>
    /// <param name="type">The type whose declaration nodes are wanted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The type's declaration syntax nodes.</returns>
    private static SyntaxNode[] DeclaringNodes(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var references = type.DeclaringSyntaxReferences;
        var nodes = new SyntaxNode[references.Length];
        for (var i = 0; i < references.Length; i++)
        {
            nodes[i] = references[i].GetSyntax(cancellationToken);
        }

        return nodes;
    }

    /// <summary>Returns whether a node is one of a set, comparing by reference.</summary>
    /// <param name="node">The node to test.</param>
    /// <param name="nodes">The set of nodes.</param>
    /// <returns><see langword="true"/> when the node is in the set.</returns>
    private static bool IsAny(SyntaxNode node, SyntaxNode[] nodes)
    {
        for (var i = 0; i < nodes.Length; i++)
        {
            if (ReferenceEquals(node, nodes[i]))
            {
                return true;
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

    /// <summary>Identifies names to find and declaring subtrees whose references are excluded.</summary>
    private readonly record struct OutsideReferenceState
    {
        /// <summary>Initializes a new instance of the <see cref="OutsideReferenceState"/> struct.</summary>
        /// <param name="name">The member name to find.</param>
        /// <param name="excludedDeclarations">The declarations whose descendants do not count as outside references.</param>
        public OutsideReferenceState(string name, SyntaxNode[] excludedDeclarations)
        {
            Name = name;
            ExcludedDeclarations = excludedDeclarations;
        }

        /// <summary>Gets the member name to find.</summary>
        public string Name { get; }

        /// <summary>Gets the declarations whose descendants are excluded.</summary>
        public SyntaxNode[] ExcludedDeclarations { get; }
    }

    /// <summary>Resolves framework attribute types on demand and caches empty results for this compilation.</summary>
    /// <param name="compilation">The compilation whose attribute types are resolved.</param>
    private sealed class PublicMandatingAttributes(Compilation compilation)
    {
        /// <summary>
        /// The metadata names of attributes whose framework requires the annotated member be <c>public</c>: narrowing
        /// such a member — all this rule could suggest — would break the framework contract, not tidy dead reach.
        /// TUnit lifecycle hooks reject any lesser accessibility (its generator demands public); Blazor binds a
        /// component parameter by reflection and requires it public.
        /// </summary>
        private static readonly string[] PublicMandatingAttributeMetadataNames =
        [
            "TUnit.Core.BeforeAttribute",
            "TUnit.Core.AfterAttribute",
            "TUnit.Core.BeforeEveryAttribute",
            "TUnit.Core.AfterEveryAttribute",
            "Microsoft.AspNetCore.Components.ParameterAttribute",
            "Microsoft.AspNetCore.Components.CascadingParameterAttribute",
            "Microsoft.AspNetCore.Components.SupplyParameterFromQueryAttribute",
        ];

        /// <summary>The resolved types, or null before the first attributed candidate.</summary>
        private INamedTypeSymbol[]? _resolved;

        /// <summary>Returns the cached types; concurrent first calls may repeat deterministic resolution.</summary>
        /// <returns>The resolved attribute types, including an empty array when none bind.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol[] Get() => _resolved ??= ResolvePublicMandatingAttributes(compilation);

        /// <summary>Resolves each public-mandating attribute type that binds in the compilation, packed with no gaps.</summary>
        /// <param name="compilation">The analyzed compilation.</param>
        /// <returns>The resolved attribute types; empty when none — such as a project referencing no such framework — bind.</returns>
        private static INamedTypeSymbol[] ResolvePublicMandatingAttributes(Compilation compilation)
        {
            var resolved = new INamedTypeSymbol[PublicMandatingAttributeMetadataNames.Length];
            var count = 0;
            for (var i = 0; i < PublicMandatingAttributeMetadataNames.Length; i++)
            {
                if (compilation.GetTypeByMetadataName(PublicMandatingAttributeMetadataNames[i]) is not { } type)
                {
                    continue;
                }

                resolved[count] = type;
                count++;
            }

            if (count == resolved.Length)
            {
                return resolved;
            }

            var trimmed = new INamedTypeSymbol[count];
            for (var i = 0; i < count; i++)
            {
                trimmed[i] = resolved[i];
            }

            return trimmed;
        }
    }
}
