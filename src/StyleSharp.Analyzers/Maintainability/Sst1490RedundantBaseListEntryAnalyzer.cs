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
/// supplies the interface, re-listing it re-maps the interface to this type's own members, so deleting the
/// entry would silently move the call back to the base implementation — or fail to compile, when the
/// entry is what allows an explicit implementation to exist. An entry implied by a base class is dropped
/// from the report when this type declares an implementation of its own. An entry implied by another
/// interface has no such risk: the type implements it directly either way.
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RedundantBaseListEntry);

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
        SyntaxNodeAnalysisContext context)
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

            impliedByBaseClass = true;
        }

        return impliedByBaseClass && !DeclaresOwnImplementation(candidate, declaration, context);
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
    /// Returns whether the type supplies its own implementation of an interface a listed base class
    /// already implements. Removing the entry would then change which member the interface reaches — or
    /// stop an explicit implementation from compiling at all — so the entry carries meaning and stays.
    /// </summary>
    /// <param name="candidate">The interface being judged.</param>
    /// <param name="declaration">The declaration owning the base list.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when this type, and not the base class, answers the interface.</returns>
    /// <remarks>
    /// An override is not an implementation of its own: it is reached through the base class's own mapping
    /// and keeps running after the entry is deleted. Every other member declared here — an explicit
    /// implementation, a <c>new</c> member that hides the base one — is a re-implementation and is kept.
    /// </remarks>
    private static bool DeclaresOwnImplementation(INamedTypeSymbol candidate, TypeDeclarationSyntax declaration, SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type)
        {
            return true;
        }

        if (DeclaresImplementationOfAnyMember(type, candidate, context.CancellationToken))
        {
            return true;
        }

        // The base-list entry also maps the interfaces it inherits, so a re-implementation of one of those
        // is lost by the same deletion.
        var inherited = candidate.AllInterfaces;
        for (var i = 0; i < inherited.Length; i++)
        {
            if (DeclaresImplementationOfAnyMember(type, inherited[i], context.CancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the type declares the member that answers any member of one interface.</summary>
    /// <param name="type">The type owning the base list.</param>
    /// <param name="interfaceType">The interface whose members are resolved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a non-override implementation is declared on this type.</returns>
    private static bool DeclaresImplementationOfAnyMember(INamedTypeSymbol type, INamedTypeSymbol interfaceType, CancellationToken cancellationToken)
    {
        var members = interfaceType.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (type.FindImplementationForInterfaceMember(members[i]) is { IsOverride: false } implementation
                && SymbolEqualityComparer.Default.Equals(implementation.ContainingType, type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Binds one base-list entry to the named type it refers to.</summary>
    /// <param name="entry">The base-list entry.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns>The named type, or <see langword="null"/> when the entry does not bind to one.</returns>
    private static INamedTypeSymbol? GetEntryType(BaseTypeSyntax entry, SyntaxNodeAnalysisContext context)
        => context.SemanticModel.GetSymbolInfo(entry.Type, context.CancellationToken).Symbol as INamedTypeSymbol;
}
