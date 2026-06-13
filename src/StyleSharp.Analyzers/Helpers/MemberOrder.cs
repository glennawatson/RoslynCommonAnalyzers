// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace StyleSharp.Analyzers;

/// <summary>
/// The ordering rank of a type member, computed once from syntax. Members compare
/// lexicographically by the default precedence — kind, accessibility,
/// constant, static, readonly — and each dimension maps to a diagnostic.
/// </summary>
/// <param name="Kind">The member-kind rank (fields first … nested unions last).</param>
/// <param name="Access">The accessibility rank (public 0 … private 5).</param>
/// <param name="Constant">0 for a constant, 1 otherwise.</param>
/// <param name="Static">0 for static (or const), 1 for instance.</param>
/// <param name="ReadOnly">0 for a readonly field, 1 otherwise.</param>
internal readonly record struct MemberOrder(int Kind, int Access, int Constant, int Static, int ReadOnly)
{
    /// <summary>The kind rank for a field.</summary>
    private const int FieldKind = 0;

    /// <summary>The kind rank for a constructor.</summary>
    private const int ConstructorKind = 1;

    /// <summary>The kind rank for a finalizer.</summary>
    private const int DestructorKind = 2;

    /// <summary>The kind rank for a delegate.</summary>
    private const int DelegateKind = 3;

    /// <summary>The kind rank for an event.</summary>
    private const int EventKind = 4;

    /// <summary>The kind rank for an enum.</summary>
    private const int EnumKind = 5;

    /// <summary>The kind rank for a nested interface.</summary>
    private const int InterfaceKind = 6;

    /// <summary>The kind rank for a property.</summary>
    private const int PropertyKind = 7;

    /// <summary>The kind rank for an indexer.</summary>
    private const int IndexerKind = 8;

    /// <summary>The kind rank for a method, operator, or conversion.</summary>
    private const int MethodKind = 9;

    /// <summary>The kind rank for a nested struct (including record structs).</summary>
    private const int StructKind = 10;

    /// <summary>The kind rank for a nested class.</summary>
    private const int ClassKind = 11;

    /// <summary>The kind rank for a nested record.</summary>
    private const int RecordKind = 12;

    /// <summary>The kind rank for a nested union (after classes and records).</summary>
    private const int UnionKind = 13;

    /// <summary>The sentinel rank for a member that is not ordered.</summary>
    private const int NoKind = -1;

    /// <summary>The metadata name of the marker interface that identifies a union.</summary>
    private const string UnionMarkerMetadataName = "System.Runtime.CompilerServices.IUnion";

    /// <summary>The accessibility rank for a public member.</summary>
    private const int PublicAccess = 0;

    /// <summary>The accessibility rank for an internal member.</summary>
    private const int InternalAccess = 1;

    /// <summary>The accessibility rank for a protected internal member.</summary>
    private const int ProtectedInternalAccess = 2;

    /// <summary>The accessibility rank for a protected member.</summary>
    private const int ProtectedAccess = 3;

    /// <summary>The accessibility rank for a private protected member.</summary>
    private const int PrivateProtectedAccess = 4;

    /// <summary>The accessibility rank for a private member.</summary>
    private const int PrivateAccess = 5;

    /// <summary>Classifies a member's ordering rank, or returns <see langword="null"/> to skip it (e.g. explicit interface impls).</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="isUnion">Whether a nested class/record member is a union (detected semantically by the caller).</param>
    /// <returns>The rank, or <see langword="null"/>.</returns>
    public static MemberOrder? Classify(MemberDeclarationSyntax member, bool isUnion = false)
    {
        if (HasExplicitInterfaceSpecifier(member))
        {
            return null;
        }

        var kind = KindRank(member.Kind(), isUnion);
        if (kind == NoKind)
        {
            return null;
        }

        var facts = ReadModifierFacts(member.Modifiers);
        var isConst = facts.IsConst;
        var isStatic = isConst || facts.IsStatic;

        // A static constructor has no access modifier and is implicitly "private", but it always
        // belongs immediately before the instance constructors regardless of their accessibility.
        // Rank it as public so its placement before a public/internal instance constructor is not
        // flagged by the access dimension (SST1202); the static dimension still orders it first.
        var access = kind == ConstructorKind && facts.IsStatic
            ? PublicAccess
            : AccessRank(facts, member.Parent is InterfaceDeclarationSyntax);

        // The readonly ordering rules (SST1214/SST1215) apply only to fields. 'readonly' on a method
        // or struct (e.g. 'public readonly bool Equals(...)', 'readonly struct') must not be treated as
        // a readonly field, so non-field members stay in the non-readonly rank.
        return new MemberOrder(
            kind,
            access,
            isConst ? 0 : 1,
            isStatic ? 0 : 1,
            facts.IsReadOnly && kind == FieldKind ? 0 : 1);
    }

    /// <summary>Resolves the <c>IUnion</c> marker interface in a compilation, or <see langword="null"/> when no unions are present.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>The marker symbol, or <see langword="null"/>.</returns>
    public static INamedTypeSymbol? ResolveUnionMarker(Compilation compilation)
        => compilation.GetTypeByMetadataName(UnionMarkerMetadataName);

    /// <summary>Returns whether a nested class/record member is a union (implements the marker interface).</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="model">The semantic model for the member's tree.</param>
    /// <param name="marker">The resolved <c>IUnion</c> marker symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the member is a union type.</returns>
    public static bool IsUnion(MemberDeclarationSyntax member, SemanticModel model, INamedTypeSymbol marker, CancellationToken cancellationToken)
    {
        var isReferenceType = member is ClassDeclarationSyntax
            || (member is RecordDeclarationSyntax record && !record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword));
        if (!isReferenceType)
        {
            return false;
        }

        if (model.GetDeclaredSymbol(member, cancellationToken) is not INamedTypeSymbol symbol)
        {
            return false;
        }

        foreach (var iface in symbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, marker))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Compares this rank with another in the default precedence order.</summary>
    /// <param name="other">The rank to compare against.</param>
    /// <returns>Negative when this sorts first, positive when last, zero when equal.</returns>
    public int CompareTo(in MemberOrder other) => CompareDimensions(this, other);

    /// <summary>Returns the rule violated when this member follows <paramref name="previous"/>, or <see langword="null"/> when in order.</summary>
    /// <param name="previous">The preceding member's rank.</param>
    /// <returns>The violated rule, or <see langword="null"/>.</returns>
    public DiagnosticDescriptor? ViolationAfter(in MemberOrder previous) => SelectViolationRule(this, previous);

    /// <summary>Returns the token to report on (and name) for a member.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The representative identifier token.</returns>
    public static SyntaxToken NameToken(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => FirstVariable(field.Declaration),
        EventFieldDeclarationSyntax @event => FirstVariable(@event.Declaration),
        IndexerDeclarationSyntax indexer => indexer.ThisKeyword,
        OperatorDeclarationSyntax @operator => @operator.OperatorToken,
        ConversionOperatorDeclarationSyntax conversion => conversion.OperatorKeyword,
        BaseTypeDeclarationSyntax type => type.Identifier,
        _ => NamedMemberToken(member)
    };

    /// <summary>Reads the relevant modifier facts from one token-list scan.</summary>
    /// <param name="modifiers">The modifiers to inspect.</param>
    /// <returns>The gathered modifier facts.</returns>
    internal static ModifierFacts ReadModifierFacts(SyntaxTokenList modifiers)
    {
        var isPublic = false;
        var isInternal = false;
        var isProtected = false;
        var isPrivate = false;
        var isConst = false;
        var isStatic = false;
        var isReadOnly = false;

        for (var i = 0; i < modifiers.Count; i++)
        {
            switch (modifiers[i].Kind())
            {
                case SyntaxKind.PublicKeyword:
                {
                    isPublic = true;
                    break;
                }

                case SyntaxKind.InternalKeyword:
                {
                    isInternal = true;
                    break;
                }

                case SyntaxKind.ProtectedKeyword:
                {
                    isProtected = true;
                    break;
                }

                case SyntaxKind.PrivateKeyword:
                {
                    isPrivate = true;
                    break;
                }

                case SyntaxKind.ConstKeyword:
                {
                    isConst = true;
                    break;
                }

                case SyntaxKind.StaticKeyword:
                {
                    isStatic = true;
                    break;
                }

                case SyntaxKind.ReadOnlyKeyword:
                {
                    isReadOnly = true;
                    break;
                }
            }
        }

        return new(isPublic, isInternal, isProtected, isPrivate, isConst, isStatic, isReadOnly);
    }

    /// <summary>Compares two member-order values in the default precedence order.</summary>
    /// <param name="left">The left order.</param>
    /// <param name="right">The right order.</param>
    /// <returns>Negative when <paramref name="left"/> sorts first, positive when last, zero when equal.</returns>
    internal static int CompareDimensions(in MemberOrder left, in MemberOrder right)
    {
        var difference = left.Kind - right.Kind;
        if (difference != 0)
        {
            return difference;
        }

        difference = left.Access - right.Access;
        if (difference != 0)
        {
            return difference;
        }

        difference = left.Constant - right.Constant;
        if (difference != 0)
        {
            return difference;
        }

        difference = left.Static - right.Static;
        if (difference != 0)
        {
            return difference;
        }

        return left.ReadOnly - right.ReadOnly;
    }

    /// <summary>Returns the member-kind rank for a syntax kind, or <see cref="NoKind"/> when it is not ordered.</summary>
    /// <param name="kind">The member declaration kind.</param>
    /// <param name="isUnion">Whether a nested class/record member is a union.</param>
    /// <returns>The kind rank.</returns>
    [SuppressMessage(
        "Major Code Smell",
        "the rule:Cyclomatic Complexity of methods should not be too high",
        Justification = "A direct switch-based kind map benchmarked better than the dictionary-backed alternatives on the MemberOrdering hot path.")]
    internal static int KindRank(SyntaxKind kind, bool isUnion) =>
        isUnion
            ? UnionKind
            : kind switch
            {
                SyntaxKind.FieldDeclaration => FieldKind,
                SyntaxKind.ConstructorDeclaration => ConstructorKind,
                SyntaxKind.DestructorDeclaration => DestructorKind,
                SyntaxKind.DelegateDeclaration => DelegateKind,
                SyntaxKind.EventDeclaration or SyntaxKind.EventFieldDeclaration => EventKind,
                SyntaxKind.EnumDeclaration => EnumKind,
                SyntaxKind.InterfaceDeclaration => InterfaceKind,
                SyntaxKind.PropertyDeclaration => PropertyKind,
                SyntaxKind.IndexerDeclaration => IndexerKind,
                SyntaxKind.MethodDeclaration or SyntaxKind.OperatorDeclaration or SyntaxKind.ConversionOperatorDeclaration => MethodKind,
                SyntaxKind.StructDeclaration or SyntaxKind.RecordStructDeclaration => StructKind,
                SyntaxKind.ClassDeclaration => ClassKind,
                SyntaxKind.RecordDeclaration => RecordKind,
                _ => NoKind
            };

    /// <summary>Returns the violated ordering rule when the current member follows the previous member.</summary>
    /// <param name="current">The current member order.</param>
    /// <param name="previous">The previous member order.</param>
    /// <returns>The violated rule, or <see langword="null"/> when the members are already ordered.</returns>
    internal static DiagnosticDescriptor? SelectViolationRule(in MemberOrder current, in MemberOrder previous) =>
        !TrySelectRuleForDifference(current.Kind - previous.Kind, OrderingRules.OrderByKind, out var rule)
        && !TrySelectRuleForDifference(current.Access - previous.Access, OrderingRules.OrderByAccess, out rule)
        && !TrySelectRuleForDifference(current.Constant - previous.Constant, OrderingRules.ConstantsBeforeFields, out rule)
        && !TrySelectRuleForDifference(current.Static - previous.Static, OrderingRules.StaticBeforeInstance, out rule)

        // Readonly ordering is the last tiebreaker: only consulted when every earlier dimension is
        // equal. A const sorts before a readonly field via the Constant dimension, so the chain stops
        // there and the readonly check never runs across them.
        && !TrySelectReadonlyRule(current, previous, out rule)
            ? null
            : rule;

    /// <summary>Returns the identifier token for the member kinds that carry a plain name.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The identifier token, or the member's first token as a fallback.</returns>
    private static SyntaxToken NamedMemberToken(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier,
        ConstructorDeclarationSyntax constructor => constructor.Identifier,
        DestructorDeclarationSyntax destructor => destructor.Identifier,
        PropertyDeclarationSyntax property => property.Identifier,
        EventDeclarationSyntax @event => @event.Identifier,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier,
        _ => member.GetFirstToken()
    };

    /// <summary>Returns the identifier of the first variable in a field/event declaration.</summary>
    /// <param name="declaration">The variable declaration.</param>
    /// <returns>The first variable's identifier, or the declaration's first token.</returns>
    private static SyntaxToken FirstVariable(VariableDeclarationSyntax declaration)
        => declaration.Variables.Count > 0 ? declaration.Variables[0].Identifier : declaration.GetFirstToken();

    /// <summary>Returns whether a member is an explicit interface implementation (skipped for ordering).</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member has an explicit interface specifier.</returns>
    private static bool HasExplicitInterfaceSpecifier(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier is not null,
        PropertyDeclarationSyntax property => property.ExplicitInterfaceSpecifier is not null,
        EventDeclarationSyntax @event => @event.ExplicitInterfaceSpecifier is not null,
        IndexerDeclarationSyntax indexer => indexer.ExplicitInterfaceSpecifier is not null,
        _ => false
    };

    /// <summary>Routes a readonly-ordering violation to the instance variant (SST1215) for instance fields.</summary>
    /// <param name="order">The member order that violated readonly ordering.</param>
    /// <returns>SST1215 for an instance readonly violation, otherwise SST1214.</returns>
    private static DiagnosticDescriptor ReadonlyViolationRule(in MemberOrder order)
        => order.Static == 1
            ? OrderingRules.InstanceReadonlyBeforeNonReadonly
            : OrderingRules.ReadonlyBeforeNonReadonly;

    /// <summary>Selects the readonly-ordering rule when the readonly dimension determines ordering and is violated.</summary>
    /// <param name="current">The current member order.</param>
    /// <param name="previous">The previous member order.</param>
    /// <param name="selectedRule">The readonly rule when the current member sorts earlier; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the readonly dimension determines ordering.</returns>
    private static bool TrySelectReadonlyRule(in MemberOrder current, in MemberOrder previous, out DiagnosticDescriptor? selectedRule)
    {
        var difference = current.ReadOnly - previous.ReadOnly;
        if (difference == 0)
        {
            selectedRule = null;
            return false;
        }

        selectedRule = difference < 0 ? ReadonlyViolationRule(current) : null;
        return true;
    }

    /// <summary>Returns whether a dimension difference determines ordering, and selects its rule when violated.</summary>
    /// <param name="difference">The difference between the current and previous dimension values.</param>
    /// <param name="rule">The rule associated with the dimension.</param>
    /// <param name="selectedRule">The selected rule when the current member sorts earlier; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when this dimension determines ordering and later dimensions must not be consulted.</returns>
    private static bool TrySelectRuleForDifference(int difference, DiagnosticDescriptor rule, out DiagnosticDescriptor? selectedRule)
    {
        if (difference == 0)
        {
            selectedRule = null;
            return false;
        }

        selectedRule = difference < 0 ? rule : null;
        return true;
    }

    /// <summary>Computes the accessibility rank from a member's modifiers.</summary>
    /// <param name="facts">The gathered member modifier facts.</param>
    /// <param name="inInterface">Whether the member is declared in an interface (default public).</param>
    /// <returns>The accessibility rank (most accessible first).</returns>
    private static int AccessRank(ModifierFacts facts, bool inInterface)
    {
        if (facts.IsPublic)
        {
            return PublicAccess;
        }

        switch (facts.IsProtected)
        {
            case true when facts.IsInternal:
                return ProtectedInternalAccess;
            case true when facts.IsPrivate:
                return PrivateProtectedAccess;
        }

        if (facts.IsInternal)
        {
            return InternalAccess;
        }

        if (facts.IsProtected)
        {
            return ProtectedAccess;
        }

        // No access modifier: interface members are public; everything else is private.
        return facts.IsPrivate || !inInterface ? PrivateAccess : PublicAccess;
    }

    /// <summary>Modifier facts gathered from one token-list scan.</summary>
    internal readonly record struct ModifierFacts(
        bool IsPublic,
        bool IsInternal,
        bool IsProtected,
        bool IsPrivate,
        bool IsConst,
        bool IsStatic,
        bool IsReadOnly);
}
