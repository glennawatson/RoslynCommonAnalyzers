// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a base-list entry that another entry in the same list already brings in (SST1490): an
/// interface a listed base class implements, or an interface a listed interface inherits.
/// </summary>
/// <remarks>
/// <para>
/// The reason is always visible in the list being read: an entry is reported only when another entry of
/// the <em>same</em> base list implies it. A partial type whose parts each list one thing is therefore
/// never reported for what a different part says — a base list of one entry cannot imply anything, so the
/// walk stops before the semantic model is touched.
/// </para>
/// <para>
/// The one shape that survives the redundancy is interface re-implementation. When a base class already
/// supplies the interface, re-listing it restarts the interface mapping at this type, so deleting the entry
/// would silently move the call back to the member the base class maps — or fail to compile, when the entry
/// is what allows an explicit implementation to exist. An entry implied by a base class is therefore dropped
/// from the report only when both mappings reach the same member. An entry implied by another interface has
/// no such risk: the type implements it directly either way.
/// </para>
/// <para>
/// An explicit <c>object</c> base is not reported here; SST1177 already covers the compiler-implied base
/// type and the compiler-implied enum underlying type.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1490RedundantBaseListEntryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The smallest base list in which one entry can imply another.</summary>
    private const int MinimumImplyingEntryCount = 2;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.RedundantBaseListEntry);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Reports every entry of one base list that the rest of that list already implies.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <remarks>
    /// The entry count is the whole clean path: a type with no base list, or with a single entry, is
    /// rejected on syntax alone. Symbols are bound one entry at a time inside the comparison rather than
    /// gathered into an array first, so the pass stays allocation-free; the lists are two or three entries
    /// long and the semantic model caches each bind.
    /// </remarks>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (declaration.BaseList is not { } baseList || baseList.Types.Count < MinimumImplyingEntryCount)
        {
            return;
        }

        var entries = baseList.Types;
        for (var i = 0; i < entries.Count; i++)
        {
            if (GetEntryType(entries[i], context) is not { TypeKind: TypeKind.Interface } candidate)
            {
                continue;
            }

            if (IsImpliedByAnotherEntry(entries, i, candidate, declaration, context))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    MaintainabilityRules.RedundantBaseListEntry,
                    entries[i].GetLocation(),
                    entries[i].Type.ToString()));
            }
        }
    }

    /// <summary>Returns whether another entry of the same base list already brings the candidate in.</summary>
    /// <param name="entries">The base list's entries.</param>
    /// <param name="candidateIndex">The index of the entry being judged.</param>
    /// <param name="candidate">The interface the entry names.</param>
    /// <param name="declaration">The declaration owning the base list.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when the entry states what the list already says.</returns>
    private static bool IsImpliedByAnotherEntry(
        SeparatedSyntaxList<BaseTypeSyntax> entries,
        int candidateIndex,
        INamedTypeSymbol candidate,
        TypeDeclarationSyntax declaration,
        in SyntaxNodeAnalysisContext context)
    {
        var impliedByBaseClass = false;
        for (var i = 0; i < entries.Count; i++)
        {
            if (i == candidateIndex || GetEntryType(entries[i], context) is not { } other || !Brings(other, candidate))
            {
                continue;
            }

            // An interface that another interface in the list inherits is implied with nothing else to
            // check: the type implements it directly whether or not the entry is written down.
            if (other.TypeKind != TypeKind.Class)
            {
                return true;
            }

            // A base list names at most one class, so this runs once: the entry is redundant unless
            // re-implementing the interface here reaches a different member than the base class does.
            impliedByBaseClass = !ReimplementationChangesDispatch(candidate, other, declaration, context);
        }

        return impliedByBaseClass;
    }

    /// <summary>Returns whether one base-list entry's type carries the candidate interface.</summary>
    /// <param name="entry">The other entry's type.</param>
    /// <param name="candidate">The interface being judged.</param>
    /// <returns><see langword="true"/> when the entry already implements or inherits the interface.</returns>
    private static bool Brings(INamedTypeSymbol entry, INamedTypeSymbol candidate)
    {
        var interfaces = entry.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether re-implementing the interface here sends it to a different member than the base
    /// class's own mapping does. Removing the entry would then change which member the interface reaches —
    /// or stop an explicit implementation from compiling at all — so the entry carries meaning and stays.
    /// </summary>
    /// <param name="candidate">The interface being judged.</param>
    /// <param name="baseClass">The listed base class that already implements the interface.</param>
    /// <param name="declaration">The declaration owning the base list.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when the entry decides which member the interface reaches.</returns>
    /// <remarks>
    /// The member that answers the interface need not be declared here: re-implementation restarts the
    /// search at this type, so it also picks up a <c>new</c> member anywhere in the base chain that the
    /// base class's own mapping skipped over. The two mappings are compared through the override chain,
    /// because an override is reached by virtual dispatch through the base class's mapping and keeps
    /// running after the entry is deleted.
    /// </remarks>
    private static bool ReimplementationChangesDispatch(
        INamedTypeSymbol candidate,
        INamedTypeSymbol baseClass,
        TypeDeclarationSyntax declaration,
        in SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type)
        {
            return true;
        }

        if (MappingDiffersFromBaseClass(type, baseClass, candidate, context.CancellationToken))
        {
            return true;
        }

        // The base-list entry also maps the interfaces it inherits, so a re-implementation of one of those
        // is lost by the same deletion.
        var inherited = candidate.AllInterfaces;
        for (var i = 0; i < inherited.Length; i++)
        {
            if (MappingDiffersFromBaseClass(type, baseClass, inherited[i], context.CancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether this type maps any member of one interface elsewhere than its base class does.</summary>
    /// <param name="type">The type owning the base list.</param>
    /// <param name="baseClass">The listed base class that already implements the interface.</param>
    /// <param name="interfaceType">The interface whose members are resolved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the two mappings disagree on a member.</returns>
    private static bool MappingDiffersFromBaseClass(
        INamedTypeSymbol type,
        INamedTypeSymbol baseClass,
        INamedTypeSymbol interfaceType,
        CancellationToken cancellationToken)
    {
        var members = interfaceType.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var here = BaseDeclarationOf(type.FindImplementationForInterfaceMember(members[i]));
            var inherited = BaseDeclarationOf(baseClass.FindImplementationForInterfaceMember(members[i]));
            if (!SymbolEqualityComparer.Default.Equals(here, inherited))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Walks an override back to the member it ultimately overrides.</summary>
    /// <param name="implementation">The member a mapping resolved to, if any.</param>
    /// <returns>The base-most declaration of that member.</returns>
    /// <remarks>
    /// Two mappings that land on different links of one override chain reach the same code at run time, so
    /// the chain is collapsed before they are compared.
    /// </remarks>
    private static ISymbol? BaseDeclarationOf(ISymbol? implementation)
    {
        while (true)
        {
            var overridden = implementation switch
            {
                IMethodSymbol { IsOverride: true } method => method.OverriddenMethod,
                IPropertySymbol { IsOverride: true } property => property.OverriddenProperty,
                IEventSymbol { IsOverride: true } @event => (ISymbol?)@event.OverriddenEvent,
                _ => null,
            };

            if (overridden is null)
            {
                return implementation;
            }

            implementation = overridden;
        }
    }

    /// <summary>Binds one base-list entry to the named type it refers to.</summary>
    /// <param name="entry">The base-list entry.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns>The named type, or <see langword="null"/> when the entry does not bind to one.</returns>
    private static INamedTypeSymbol? GetEntryType(BaseTypeSyntax entry, in SyntaxNodeAnalysisContext context) =>
        context.SemanticModel.GetSymbolInfo(entry.Type, context.CancellationToken).Symbol as INamedTypeSymbol;
}
