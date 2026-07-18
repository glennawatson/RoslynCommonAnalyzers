// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member declared with <c>new</c> (SST2462) whose accessibility is strictly narrower than the
/// inherited member it hides. Hiding is resolved against the static type of the reference, so a caller
/// holding the base type still binds to the more accessible inherited member: the narrowed accessibility
/// is not enforced and the hierarchy exposes two members that diverge.
/// </summary>
/// <remarks>
/// The walk is symbol-based and its clean path never touches syntax. A type whose base is <c>object</c> is
/// rejected outright; for every other type each declared member is matched against a same-named — and, for
/// methods, same-signature — accessible inherited member up the base chain. Only once a hidden member that
/// is strictly more accessible has been found is the declaration's <c>new</c> modifier read, so the syntax
/// is fetched only on the verge of reporting. A member hidden without <c>new</c> is a compiler diagnostic
/// and is not reported here; a <c>new</c> member that hides nothing, or that is equally or more accessible,
/// is never reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2462NewMemberReducesAccessibilityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Caller category: a derived type in the same assembly.</summary>
    private const int SameAssemblyDerived = 0b0001;

    /// <summary>Caller category: a derived type in another assembly.</summary>
    private const int OtherAssemblyDerived = 0b0010;

    /// <summary>Caller category: a non-derived type in the same assembly.</summary>
    private const int SameAssemblyOther = 0b0100;

    /// <summary>Caller category: a non-derived type in another assembly.</summary>
    private const int OtherAssemblyOther = 0b1000;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.NewMemberReducesAccessibility);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Examines a class's own members for one whose <c>new</c> narrows a more accessible inherited member.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.BaseType is null or { SpecialType: SpecialType.System_Object })
        {
            return;
        }

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (!IsHideableMember(member))
            {
                continue;
            }

            if (FindNarrowedHiddenMember(type, member) is not { } hidden)
            {
                continue;
            }

            // The declaration is read only now, once a narrowed hidden member has been found — never on
            // the clean path. A member hidden without 'new' is the compiler's CS0108, not ours.
            if (!DeclaresNewModifier(member))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                CorrectnessRules.NewMemberReducesAccessibility,
                member.Locations[0],
                member.Name,
                AccessibilityKeyword(hidden.DeclaredAccessibility),
                AccessibilityKeyword(member.DeclaredAccessibility),
                hidden.ContainingType.Name));
        }
    }

    /// <summary>Returns whether a member is one of the kinds that can carry <c>new</c> and hide an inherited member.</summary>
    /// <param name="member">The declared member.</param>
    /// <returns><see langword="true"/> for a non-override method, property, event, field, or nested type.</returns>
    private static bool IsHideableMember(ISymbol member)
    {
        if (member.IsOverride || member.IsImplicitlyDeclared)
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

    /// <summary>Walks the base chain for the nearest inherited member this one hides and is strictly less accessible than.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="member">The declared member under test.</param>
    /// <returns>The narrowed hidden member, or <see langword="null"/> when none is hidden or the accessibility is not reduced.</returns>
    private static ISymbol? FindNarrowedHiddenMember(INamedTypeSymbol type, ISymbol member)
    {
        var derivedAccessibility = member.DeclaredAccessibility;
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            // The nearest base type carrying a hidden member is what a reference of that type binds to, so
            // the search stops there whether or not its accessibility turns out to be reduced.
            if (FindHiddenMemberIn(baseType, member) is { } hidden)
            {
                return IsNarrower(derivedAccessibility, hidden.DeclaredAccessibility) ? hidden : null;
            }
        }

        return null;
    }

    /// <summary>Returns the most accessible member of a single base type that the derived member hides, if any.</summary>
    /// <param name="baseType">The base type to search.</param>
    /// <param name="member">The declared member under test.</param>
    /// <returns>The most accessible hidden member, or <see langword="null"/> when the base type hides nothing.</returns>
    private static ISymbol? FindHiddenMemberIn(INamedTypeSymbol baseType, ISymbol member)
    {
        var candidates = baseType.GetMembers(member.Name);
        ISymbol? hidden = null;
        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];

            // A private base member is not inherited into an accessible scope, so nothing hides it.
            if (candidate.DeclaredAccessibility == Accessibility.Private || !Hides(member, candidate))
            {
                continue;
            }

            // Keep the most accessible hidden member: it is the one a base-typed reference would reach.
            if (hidden is null || IsNarrower(hidden.DeclaredAccessibility, candidate.DeclaredAccessibility))
            {
                hidden = candidate;
            }
        }

        return hidden;
    }

    /// <summary>Returns whether the derived member hides the inherited candidate of the same name.</summary>
    /// <param name="member">The declared member.</param>
    /// <param name="candidate">The same-named inherited member.</param>
    /// <returns><see langword="true"/> when C# member hiding applies between the two.</returns>
    private static bool Hides(ISymbol member, ISymbol candidate)
    {
        // A method hides an inherited method only when their signatures match; a nested type hides a base
        // type of the same arity. Every other member kind hides an inherited member of its own kind by name.
        if (member is IMethodSymbol derivedMethod)
        {
            return candidate is IMethodSymbol baseMethod && SameSignature(derivedMethod, baseMethod);
        }

        if (member is INamedTypeSymbol derivedType)
        {
            return candidate is INamedTypeSymbol baseNestedType && derivedType.Arity == baseNestedType.Arity;
        }

        return member.Kind == candidate.Kind;
    }

    /// <summary>Returns whether two methods share a hiding signature (arity, parameter types, and ref kinds).</summary>
    /// <param name="derived">The derived method.</param>
    /// <param name="candidate">The candidate base method.</param>
    /// <returns><see langword="true"/> when the methods hide one another by signature.</returns>
    private static bool SameSignature(IMethodSymbol derived, IMethodSymbol candidate)
    {
        if (derived.Arity != candidate.Arity || derived.Parameters.Length != candidate.Parameters.Length)
        {
            return false;
        }

        var derivedParameters = derived.Parameters;
        var candidateParameters = candidate.Parameters;
        for (var i = 0; i < derivedParameters.Length; i++)
        {
            if (derivedParameters[i].RefKind != candidateParameters[i].RefKind
                || !SymbolEqualityComparer.Default.Equals(derivedParameters[i].Type, candidateParameters[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether one accessibility admits a strict subset of the callers another admits.</summary>
    /// <param name="candidate">The accessibility under test.</param>
    /// <param name="reference">The accessibility to compare against.</param>
    /// <returns><see langword="true"/> when <paramref name="candidate"/> is strictly less permissive than <paramref name="reference"/>.</returns>
    private static bool IsNarrower(Accessibility candidate, Accessibility reference)
    {
        // 'protected' and 'internal' admit disjoint caller sets, so neither is narrower than the other; the
        // subset test below returns false for that pair and true only for a strict subset.
        var candidateReach = AccessibilityReach(candidate);
        var referenceReach = AccessibilityReach(reference);
        return candidateReach != referenceReach && (candidateReach & referenceReach) == candidateReach;
    }

    /// <summary>Returns the set of caller categories an accessibility admits, as a bit mask.</summary>
    /// <param name="accessibility">The accessibility to model.</param>
    /// <returns>A mask over {same-assembly derived, other-assembly derived, same-assembly other, other-assembly other}.</returns>
    private static int AccessibilityReach(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => SameAssemblyDerived | OtherAssemblyDerived | SameAssemblyOther | OtherAssemblyOther,
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

    /// <summary>Returns whether a member's declaration carries the <c>new</c> modifier.</summary>
    /// <param name="member">The member to test.</param>
    /// <returns><see langword="true"/> when the author declared the member with <c>new</c>.</returns>
    private static bool DeclaresNewModifier(ISymbol member)
    {
        var references = member.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            var node = references[i].GetSyntax();

            // A field or field-like event points at its declarator; the modifiers sit on the enclosing
            // field/event declaration two nodes up.
            if (node is VariableDeclaratorSyntax variable)
            {
                node = variable.Parent?.Parent;
            }

            if (node is MemberDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.NewKeyword))
            {
                return true;
            }
        }

        return false;
    }
}
