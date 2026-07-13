// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an overload that other members separate from the rest of its family (SST1218). The diagnostic
/// lands on the overload that appears after the interruption — the one that is out of place.
/// </summary>
/// <remarks>
/// <para>
/// Two methods count as one family when they share a name <em>and</em> the place the ordering rules give
/// them: the same accessibility and the same static-ness. Comparing across those lines would fight the
/// member-ordering rules, which require a private overload after the public ones and a static member before
/// the instance ones — telling an author to move a member the ordering rules have already placed is not a
/// fix, it is a loop.
/// </para>
/// <para>
/// Constructors are not measured. They already share the type's name, so a reader who found one knows where
/// the rest live, and the ordering rules pin them to one block of the type anyway. Operators, indexers and
/// conversions are excluded for the same reason. An explicit interface implementation is excluded because
/// its placement follows the interface it implements, not the method name it happens to share.
/// </para>
/// <para>
/// Each type declaration is judged on its own members, so the parts of a partial type never pull each other
/// out of order. Comments and blank lines between two overloads are not members, so they never separate a
/// family.
/// </para>
/// <para>
/// The rule is pure syntax: identifier text and modifier keywords, no semantic model, and no allocation on
/// any path.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1218OverloadsGroupedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The modifiers that decide where the ordering rules put a member.</summary>
    [Flags]
    private enum Placements
    {
        /// <summary>The member declares none of them.</summary>
        None = 0,

        /// <summary>The member is <c>public</c>.</summary>
        Public = 1,

        /// <summary>The member is <c>protected</c>.</summary>
        Protected = 2,

        /// <summary>The member is <c>internal</c>.</summary>
        Internal = 4,

        /// <summary>The member is <c>private</c>.</summary>
        Private = 8,

        /// <summary>The member is <c>static</c>.</summary>
        Static = 16,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(OrderingRules.OverloadsGrouped);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.InterfaceDeclaration);
    }

    /// <summary>Finds the overload that an out-of-place overload should sit beside.</summary>
    /// <param name="members">The type's members.</param>
    /// <param name="index">The index of the candidate overload.</param>
    /// <param name="anchorIndex">The index of the nearest preceding overload of the same family.</param>
    /// <returns><see langword="true"/> when the member at <paramref name="index"/> is separated from its family.</returns>
    internal static bool TryFindSeparatedOverload(SyntaxList<MemberDeclarationSyntax> members, int index, out int anchorIndex)
    {
        anchorIndex = -1;
        if (index < 1 || members[index] is not MethodDeclarationSyntax method || !IsGroupable(method))
        {
            return false;
        }

        for (var candidate = index - 1; candidate >= 0; candidate--)
        {
            if (members[candidate] is not MethodDeclarationSyntax previous || !IsGroupable(previous) || !IsSameFamily(method, previous))
            {
                continue;
            }

            // The nearest earlier overload sits right above this one: the family is together.
            if (candidate == index - 1)
            {
                return false;
            }

            anchorIndex = candidate;
            return true;
        }

        return false;
    }

    /// <summary>Returns whether two method declarations belong to the same overload family.</summary>
    /// <param name="first">The first method.</param>
    /// <param name="second">The second method.</param>
    /// <returns><see langword="true"/> when they share a name and the place the ordering rules give them.</returns>
    internal static bool IsSameFamily(MethodDeclarationSyntax first, MethodDeclarationSyntax second)
        => string.Equals(first.Identifier.ValueText, second.Identifier.ValueText, StringComparison.Ordinal)
            && GetPlacement(first.Modifiers) == GetPlacement(second.Modifiers);

    /// <summary>Returns whether a method takes part in overload grouping at all.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> when the method's placement follows its name rather than an interface.</returns>
    internal static bool IsGroupable(MethodDeclarationSyntax method) => method.ExplicitInterfaceSpecifier is null;

    /// <summary>Reports every overload in the type that its family does not sit beside.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var members = ((TypeDeclarationSyntax)context.Node).Members;
        for (var i = 1; i < members.Count; i++)
        {
            if (!TryFindSeparatedOverload(members, i, out _))
            {
                continue;
            }

            var identifier = ((MethodDeclarationSyntax)members[i]).Identifier;
            context.ReportDiagnostic(DiagnosticHelper.Create(
                OrderingRules.OverloadsGrouped,
                identifier.GetLocation(),
                identifier.ValueText));
        }
    }

    /// <summary>Reads the modifiers that decide where the ordering rules put a member.</summary>
    /// <param name="modifiers">The method's modifiers.</param>
    /// <returns>The accessibility keywords the member declares, and whether it is static.</returns>
    /// <remarks>
    /// The keywords are compared, not the accessibility they resolve to, so a member that declares none is
    /// only ever grouped with another that declares none — which is the same default, whatever the type is.
    /// </remarks>
    private static Placements GetPlacement(SyntaxTokenList modifiers)
    {
        var placement = Placements.None;
        for (var i = 0; i < modifiers.Count; i++)
        {
            placement |= modifiers[i].RawKind switch
            {
                (int)SyntaxKind.PublicKeyword => Placements.Public,
                (int)SyntaxKind.ProtectedKeyword => Placements.Protected,
                (int)SyntaxKind.InternalKeyword => Placements.Internal,
                (int)SyntaxKind.PrivateKeyword => Placements.Private,
                (int)SyntaxKind.StaticKeyword => Placements.Static,
                _ => Placements.None,
            };
        }

        return placement;
    }
}
