// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Counts direct descendants per base type across a compilation so SST2337 can tell, at compilation
/// end, which abstract bases already have a fully known set of subtypes.
/// </summary>
/// <remarks>
/// Whether a hierarchy is complete is not a fact about any single declaration, so the answer only
/// exists once every type has been seen. Symbol actions run concurrently, hence the concurrent maps.
/// </remarks>
internal sealed class ClosedHierarchyTally
{
    /// <summary>How many direct descendants make a hierarchy worth closing over.</summary>
    /// <remarks>
    /// One descendant is a base class with a single implementation, where exhaustiveness buys nothing;
    /// the modifier starts paying once a switch has more than one case to cover.
    /// </remarks>
    private const int MinimumDescendants = 2;

    /// <summary>The contextual keyword that closes a hierarchy.</summary>
    private const string ClosedModifierText = "closed";

    /// <summary>The kind the host compiler gives the <c>closed</c> keyword, or <see cref="SyntaxKind.None"/> where it has none.</summary>
    /// <remarks>
    /// Resolved through <see cref="SyntaxFacts"/> against the compiler that loaded this analyzer, so
    /// the floor build recognises the keyword when it runs on a C# 15 host even though its own
    /// compiler API cannot name that kind.
    /// </remarks>
    private static readonly SyntaxKind ClosedKeywordKind = SyntaxFacts.GetContextualKeywordKind(ClosedModifierText);

    /// <summary>The abstract bases that could carry the modifier, used as a set.</summary>
    private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _candidates = new(SymbolEqualityComparer.Default);

    /// <summary>How many direct descendants each base type has in this compilation.</summary>
    private readonly ConcurrentDictionary<INamedTypeSymbol, int> _derivedCounts = new(SymbolEqualityComparer.Default);

    /// <summary>Records what one type contributes: a candidate base, a descendant, or both.</summary>
    /// <param name="type">The declared type.</param>
    public void Observe(INamedTypeSymbol type)
    {
        if (IsCandidate(type))
        {
            _candidates.TryAdd(type, true);
        }

        var baseType = type.BaseType;
        if (baseType is null || baseType.SpecialType == SpecialType.System_Object)
        {
            return;
        }

        _derivedCounts.AddOrUpdate(baseType.OriginalDefinition, 1, static (_, count) => count + 1);
    }

    /// <summary>Reports every candidate whose descendant set is already complete.</summary>
    /// <param name="context">The compilation analysis context.</param>
    public void Report(CompilationAnalysisContext context)
    {
        foreach (var candidate in _candidates)
        {
            var type = candidate.Key;
            if (!_derivedCounts.TryGetValue(type, out var derived) || derived < MinimumDescendants)
            {
                continue;
            }

            if (Declaration(type, context.CancellationToken) is not { } declaration
                || !LanguageVersions.SupportsCSharp15(declaration)
                || HasClosedModifier(declaration))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                DesignRules.PreferClosedHierarchy,
                declaration.Identifier.GetLocation(),
                type.Name));
        }
    }

    /// <summary>Gets whether a type could carry the modifier without changing what callers may do.</summary>
    /// <param name="type">The declared type.</param>
    /// <returns><see langword="true"/> for an assembly-internal abstract class or record.</returns>
    private static bool IsCandidate(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Class
            && type.IsAbstract
            && !type.IsStatic
            && type.DeclaringSyntaxReferences.Length > 0
            && !SymbolVisibility.IsExternallyVisible(type);

    /// <summary>Gets the first declaration of a type.</summary>
    /// <param name="type">The declared type.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The declaration, or <see langword="null"/> when it is not a type declaration.</returns>
    private static TypeDeclarationSyntax? Declaration(INamedTypeSymbol type, CancellationToken cancellationToken)
        => type.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) as TypeDeclarationSyntax;

    /// <summary>Gets whether a declaration already carries the <c>closed</c> modifier.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <returns><see langword="true"/> when the modifier is present.</returns>
    private static bool HasClosedModifier(TypeDeclarationSyntax declaration)
    {
        foreach (var modifier in declaration.Modifiers)
        {
            if (IsClosedKeyword(modifier))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets whether a modifier token is the C# 15 <c>closed</c> keyword.</summary>
    /// <param name="modifier">The modifier token.</param>
    /// <returns><see langword="true"/> for the <c>closed</c> keyword.</returns>
    private static bool IsClosedKeyword(SyntaxToken modifier)
        => ClosedKeywordKind != SyntaxKind.None && modifier.IsKind(ClosedKeywordKind);
}
