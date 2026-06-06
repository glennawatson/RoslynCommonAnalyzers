// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The ordering rank of a type member, computed once from syntax. Members compare
/// lexicographically by StyleCop's default precedence — kind, accessibility,
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

    /// <summary>The kind rank of each member declaration, keyed by syntax kind.</summary>
    private static readonly Dictionary<SyntaxKind, int> KindRanks = new()
    {
        [SyntaxKind.FieldDeclaration] = FieldKind,
        [SyntaxKind.ConstructorDeclaration] = ConstructorKind,
        [SyntaxKind.DestructorDeclaration] = DestructorKind,
        [SyntaxKind.DelegateDeclaration] = DelegateKind,
        [SyntaxKind.EventDeclaration] = EventKind,
        [SyntaxKind.EventFieldDeclaration] = EventKind,
        [SyntaxKind.EnumDeclaration] = EnumKind,
        [SyntaxKind.InterfaceDeclaration] = InterfaceKind,
        [SyntaxKind.PropertyDeclaration] = PropertyKind,
        [SyntaxKind.IndexerDeclaration] = IndexerKind,
        [SyntaxKind.MethodDeclaration] = MethodKind,
        [SyntaxKind.OperatorDeclaration] = MethodKind,
        [SyntaxKind.ConversionOperatorDeclaration] = MethodKind,
        [SyntaxKind.StructDeclaration] = StructKind,
        [SyntaxKind.RecordStructDeclaration] = StructKind,
        [SyntaxKind.ClassDeclaration] = ClassKind,
        [SyntaxKind.RecordDeclaration] = RecordKind,
    };

    /// <summary>The ordering dimensions, in precedence order, paired with the rule each violates.</summary>
    private static readonly (Func<MemberOrder, int> Select, DiagnosticDescriptor Rule)[] Dimensions =
    [
        (static order => order.Kind, OrderingRules.OrderByKind),
        (static order => order.Access, OrderingRules.OrderByAccess),
        (static order => order.Constant, OrderingRules.ConstantsBeforeFields),
        (static order => order.Static, OrderingRules.StaticBeforeInstance),
        (static order => order.ReadOnly, OrderingRules.ReadonlyBeforeNonReadonly),
    ];

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

        var kind = isUnion ? UnionKind : KindRank(member);
        if (kind == NoKind)
        {
            return null;
        }

        var modifiers = member.Modifiers;
        var isConst = modifiers.Any(SyntaxKind.ConstKeyword);
        var isStatic = isConst || modifiers.Any(SyntaxKind.StaticKeyword);

        return new MemberOrder(
            kind,
            AccessRank(modifiers, member.Parent is InterfaceDeclarationSyntax),
            isConst ? 0 : 1,
            isStatic ? 0 : 1,
            modifiers.Any(SyntaxKind.ReadOnlyKeyword) ? 0 : 1);
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

    /// <summary>Compares this rank with another in StyleCop's precedence order.</summary>
    /// <param name="other">The rank to compare against.</param>
    /// <returns>Negative when this sorts first, positive when last, zero when equal.</returns>
    public int CompareTo(in MemberOrder other)
    {
        foreach (var (select, _) in Dimensions)
        {
            var difference = select(this) - select(other);
            if (difference != 0)
            {
                return difference;
            }
        }

        return 0;
    }

    /// <summary>Returns the rule violated when this member follows <paramref name="previous"/>, or <see langword="null"/> when in order.</summary>
    /// <param name="previous">The preceding member's rank.</param>
    /// <returns>The violated rule, or <see langword="null"/>.</returns>
    public DiagnosticDescriptor? ViolationAfter(in MemberOrder previous)
    {
        foreach (var (select, rule) in Dimensions)
        {
            var mine = select(this);
            var prior = select(previous);
            if (mine != prior)
            {
                return mine < prior ? rule : null;
            }
        }

        return null;
    }

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
        _ => NamedMemberToken(member),
    };

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
        _ => member.GetFirstToken(),
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
        _ => false,
    };

    /// <summary>Returns the member-kind rank for a declaration, or <see cref="NoKind"/> when it is not ordered.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The kind rank.</returns>
    private static int KindRank(MemberDeclarationSyntax member)
        => KindRanks.TryGetValue(member.Kind(), out var rank) ? rank : NoKind;

    /// <summary>Computes the accessibility rank from a member's modifiers.</summary>
    /// <param name="modifiers">The member modifiers.</param>
    /// <param name="inInterface">Whether the member is declared in an interface (default public).</param>
    /// <returns>The accessibility rank (most accessible first).</returns>
    private static int AccessRank(SyntaxTokenList modifiers, bool inInterface)
    {
        var isProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
        var isInternal = modifiers.Any(SyntaxKind.InternalKeyword);
        var isPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);

        if (modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return PublicAccess;
        }

        if (isProtected && isInternal)
        {
            return ProtectedInternalAccess;
        }

        if (isProtected && isPrivate)
        {
            return PrivateProtectedAccess;
        }

        if (isInternal)
        {
            return InternalAccess;
        }

        if (isProtected)
        {
            return ProtectedAccess;
        }

        // No access modifier: interface members are public; everything else is private.
        return isPrivate || !inInterface ? PrivateAccess : PublicAccess;
    }
}
