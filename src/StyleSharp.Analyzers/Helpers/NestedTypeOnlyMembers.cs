// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Finds the private members of one type declaration that a single nested type uses and the type itself
/// never does. Shared by the SST1498 analyzer and its code fix, so both judge a member by the same rules.
/// </summary>
/// <remarks>
/// <para>
/// The whole search runs inside one type declaration, which is what keeps the diagnostic local: a private
/// member cannot be referenced from outside the type that declares it, so every use of it is inside the
/// braces the analyzer is already looking at. A type declared <c>partial</c> is the exception — its other
/// parts are other files — and is not analyzed at all.
/// </para>
/// <para>
/// The reference walk visits each member's subtree separately, which is what tells an outer use from a
/// nested one: a name found while walking a nested type's declaration is that nested type's use of it, and
/// a name found anywhere else in the type is the type's own. Names are matched on their text before the
/// symbol is bound, so a type with a nested type but no matching identifiers never pays for a bind.
/// </para>
/// </remarks>
internal static class NestedTypeOnlyMembers
{
    /// <summary>The cached reference visitor, so the traversal allocates no delegate per member.</summary>
    private static readonly DescendantTraversalHelper.DescendantVisitor<SimpleNameSyntax, ReferenceScan> ReferenceVisitor = VisitReference;

    /// <summary>Collects the private members of a type that exactly one nested type uses.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The type declaration to analyze.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The members to report, or <see langword="null"/> when the type has none.</returns>
    public static List<NestedTypeOnlyMember>? Collect(SemanticModel model, TypeDeclarationSyntax type, CancellationToken cancellationToken)
    {
        if (ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword) || !HasNestedTypeAndCandidate(type))
        {
            return null;
        }

        var candidates = CollectCandidates(model, type, cancellationToken);
        if (candidates is null)
        {
            return null;
        }

        ScanReferences(model, type, candidates, cancellationToken);
        return SelectReportable(candidates);
    }

    /// <summary>Returns whether a type is worth analyzing at all.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns><see langword="true"/> when the type nests a type and declares a member that could move into it.</returns>
    /// <remarks>One indexed pass, no allocation and no binding: this is the check that keeps ordinary types cheap.</remarks>
    private static bool HasNestedTypeAndCandidate(TypeDeclarationSyntax type)
    {
        var members = type.Members;
        var nested = false;
        var candidate = false;
        for (var i = 0; i < members.Count && !(nested && candidate); i++)
        {
            var member = members[i];
            nested |= IsNestedType(member);
            candidate |= IsPossibleCandidate(member);
        }

        return nested && candidate;
    }

    /// <summary>Returns whether a member is a nested type a member could actually move into.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> for a nested type that has a name.</returns>
    /// <remarks>
    /// An extension block parses as a type declaration but names no type: it is a grouping inside the
    /// static class that declares it, and its identifier is empty. Nothing can move "into" it — code
    /// written there is the enclosing type's own code — so it is not a nested type for this rule's
    /// purposes. Testing the identifier rather than the node kind keeps this working on the Roslyn
    /// floor, which cannot name the extension-block syntax at all.
    /// </remarks>
    private static bool IsNestedType(SyntaxNode member)
        => member is BaseTypeDeclarationSyntax { Identifier.ValueText.Length: > 0 };

    /// <summary>Returns whether a member could be one a nested type has taken over, judged on syntax alone.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is a field, property or method that is safe to move.</returns>
    /// <remarks>
    /// A member with an attribute is never a candidate. The attribute may be the only thing that uses it — a
    /// serialization callback, a test hook, a marker something reflects over — and the analyzer cannot see
    /// what reads it. A <c>partial</c> or <c>extern</c> method is defined somewhere this rule cannot see.
    /// </remarks>
    private static bool IsPossibleCandidate(MemberDeclarationSyntax member)
    {
        if (member.AttributeLists.Count != 0)
        {
            return false;
        }

        return member switch
        {
            MethodDeclarationSyntax method => !ModifierListHelper.ContainsEither(method.Modifiers, SyntaxKind.PartialKeyword, SyntaxKind.ExternKeyword),
            FieldDeclarationSyntax or PropertyDeclarationSyntax => true,
            _ => false,
        };
    }

    /// <summary>Binds the private members of the type that could move into a nested type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The type declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The candidates, or <see langword="null"/> when the type declares none.</returns>
    private static List<NestedTypeOnlyMember>? CollectCandidates(
        SemanticModel model,
        TypeDeclarationSyntax type,
        CancellationToken cancellationToken)
    {
        List<NestedTypeOnlyMember>? candidates = null;
        var members = type.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (!IsPossibleCandidate(member))
            {
                continue;
            }

            if (member is FieldDeclarationSyntax field)
            {
                AddFieldCandidates(model, field, cancellationToken, ref candidates);
                continue;
            }

            AddCandidate(model.GetDeclaredSymbol(member, cancellationToken), member, GetIdentifier(member), ref candidates);
        }

        return candidates;
    }

    /// <summary>Adds one candidate per private variable a field declaration declares.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="field">The field declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="candidates">The candidate list, created on first use.</param>
    private static void AddFieldCandidates(
        SemanticModel model,
        FieldDeclarationSyntax field,
        CancellationToken cancellationToken,
        ref List<NestedTypeOnlyMember>? candidates)
    {
        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            AddCandidate(model.GetDeclaredSymbol(variables[i], cancellationToken), field, variables[i].Identifier, ref candidates);
        }
    }

    /// <summary>Adds one bound member to the candidate list when it is private.</summary>
    /// <param name="symbol">The member's symbol.</param>
    /// <param name="declaration">The declaration that would move.</param>
    /// <param name="identifier">The identifier the diagnostic is reported on.</param>
    /// <param name="candidates">The candidate list, created on first use.</param>
    private static void AddCandidate(
        ISymbol? symbol,
        MemberDeclarationSyntax declaration,
        SyntaxToken identifier,
        ref List<NestedTypeOnlyMember>? candidates)
    {
        if (symbol is null || symbol.DeclaredAccessibility != Accessibility.Private)
        {
            return;
        }

        candidates ??= new List<NestedTypeOnlyMember>(4);
        candidates.Add(new NestedTypeOnlyMember(symbol, declaration, identifier));
    }

    /// <summary>Gets the identifier a member declaration is named by.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The identifier token.</returns>
    private static SyntaxToken GetIdentifier(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier,
        PropertyDeclarationSyntax property => property.Identifier,
        _ => default,
    };

    /// <summary>Records every reference to a candidate, and which side of the type it came from.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The type declaration.</param>
    /// <param name="candidates">The candidates.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <remarks>
    /// The type's own header — its attributes, base list and primary constructor — is walked as the type's
    /// own use, so a member named there is never mistaken for one only a nested type wants.
    /// </remarks>
    private static void ScanReferences(
        SemanticModel model,
        TypeDeclarationSyntax type,
        List<NestedTypeOnlyMember> candidates,
        CancellationToken cancellationToken)
    {
        var children = type.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (!children[i].IsNode || children[i].AsNode() is not { } child)
            {
                continue;
            }

            var scan = IsNestedType(child)
                ? new ReferenceScan(model, candidates, Owner: null, (BaseTypeDeclarationSyntax)child, cancellationToken)
                : new ReferenceScan(model, candidates, child as MemberDeclarationSyntax, Nested: null, cancellationToken);
            DescendantTraversalHelper.VisitDescendants(child, ref scan, ReferenceVisitor);
        }
    }

    /// <summary>Records one name that may refer to a candidate.</summary>
    /// <param name="name">The referencing name.</param>
    /// <param name="scan">The running scan state.</param>
    /// <returns><see langword="true"/>, because the whole subtree is always scanned.</returns>
    private static bool VisitReference(SimpleNameSyntax name, ref ReferenceScan scan)
    {
        scan.CancellationToken.ThrowIfCancellationRequested();
        var candidates = scan.Candidates;
        var text = name.Identifier.ValueText;
        var matched = false;
        for (var i = 0; i < candidates.Count && !matched; i++)
        {
            matched = string.Equals(candidates[i].Symbol.Name, text, StringComparison.Ordinal);
        }

        if (!matched)
        {
            return true;
        }

        var symbol = scan.Model.GetSymbolInfo(name, scan.CancellationToken).Symbol;
        if (symbol is null)
        {
            return true;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (IsSameMember(symbol, candidates[i].Symbol))
            {
                RecordReference(candidates[i], name, ref scan);
            }
        }

        return true;
    }

    /// <summary>Records one resolved reference against the candidate it names.</summary>
    /// <param name="candidate">The candidate the name resolved to.</param>
    /// <param name="name">The referencing name.</param>
    /// <param name="scan">The running scan state.</param>
    private static void RecordReference(NestedTypeOnlyMember candidate, SimpleNameSyntax name, ref ReferenceScan scan)
    {
        if (scan.Nested is { } nested)
        {
            candidate.AddNestedUse(nested, IsQualified(name));
            return;
        }

        // A member that names itself — a recursive call, a field read in its own initializer — is not the
        // type using it.
        candidate.UsedByOuterType |= scan.Owner != candidate.Declaration;
    }

    /// <summary>Returns whether a name is written against a receiver rather than on its own.</summary>
    /// <param name="name">The referencing name.</param>
    /// <returns><see langword="true"/> for the <c>Helper</c> in <c>Outer.Helper()</c>.</returns>
    private static bool IsQualified(SimpleNameSyntax name)
        => name.Parent is MemberAccessExpressionSyntax member && member.Name == name;

    /// <summary>Returns whether a resolved symbol is the candidate's member.</summary>
    /// <param name="reference">The symbol the reference resolved to.</param>
    /// <param name="candidate">The candidate's symbol.</param>
    /// <returns><see langword="true"/> when both name the same member.</returns>
    private static bool IsSameMember(ISymbol reference, ISymbol candidate)
        => SymbolEqualityComparer.Default.Equals(reference, candidate)
            || SymbolEqualityComparer.Default.Equals(reference.OriginalDefinition, candidate.OriginalDefinition);

    /// <summary>Keeps the candidates that exactly one nested type uses and the type itself does not.</summary>
    /// <param name="candidates">The candidates.</param>
    /// <returns>The members to report, or <see langword="null"/> when none qualify.</returns>
    private static List<NestedTypeOnlyMember>? SelectReportable(List<NestedTypeOnlyMember> candidates)
    {
        List<NestedTypeOnlyMember>? reportable = null;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates[i].IsUsedOnlyByOneNestedType)
            {
                continue;
            }

            reportable ??= new List<NestedTypeOnlyMember>(candidates.Count);
            reportable.Add(candidates[i]);
        }

        return reportable;
    }

    /// <summary>The state threaded through one member's reference walk.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="Candidates">The candidates being tracked.</param>
    /// <param name="Owner">The outer member being walked, or <see langword="null"/> for a nested type or the type's header.</param>
    /// <param name="Nested">The nested type being walked, or <see langword="null"/> when the type's own code is.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private readonly record struct ReferenceScan(
        SemanticModel Model,
        List<NestedTypeOnlyMember> Candidates,
        MemberDeclarationSyntax? Owner,
        BaseTypeDeclarationSyntax? Nested,
        CancellationToken CancellationToken);
}
