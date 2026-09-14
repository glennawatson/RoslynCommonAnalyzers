// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves an overload back beside its family (SST1218). The member moves as a whole node, so the blank line,
/// the comments and the documentation comment in its leading trivia travel with it.
/// </summary>
/// <remarks>
/// A fix is not offered when the type contains a preprocessor directive: a member carries the directives in
/// its trivia with it, and moving one past a <c>#region</c> or a <c>#if</c> would leave the pair unbalanced.
/// Fixing one diagnostic moves one member; Fix All regroups every family in the type at once, which is the
/// same result reached in one pass rather than several.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1218OverloadsGroupedCodeFixProvider))]
[Shared]
public sealed class Sst1218OverloadsGroupedCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    /// <remarks>
    /// Every diagnostic in one type asks for the same end state, and regrouping is idempotent, so the batch
    /// composes the requests instead of trying to sequence a series of single-member moves.
    /// </remarks>
    private static readonly BatchEditFixAllProvider FixAll = new(FindRegroupableType, static (current, _) => Regroup(current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(OrderingRules.OverloadsGrouped.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => TryGetSeparatedOverload(root, diagnostic, out _, out _, out _) ? "Move the overload beside its family" : null,
            static _ => nameof(Sst1218OverloadsGroupedCodeFixProvider),
            TryRewrite);

    /// <summary>Regroups every overload family in a type, keeping each family at its first member.</summary>
    /// <param name="node">The current type declaration, including any nested batch edits.</param>
    /// <returns>The type with each family's overloads gathered together.</returns>
    private static SyntaxNode Regroup(SyntaxNode node)
    {
        if (node is not TypeDeclarationSyntax type)
        {
            return node;
        }

        var members = type.Members;
        var ordered = new List<MemberDeclarationSyntax>(members.Count);
        var placed = new bool[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            if (placed[i])
            {
                continue;
            }

            ordered.Add(members[i]);
            placed[i] = true;
            if (members[i] is MethodDeclarationSyntax method && Sst1218OverloadsGroupedAnalyzer.IsGroupable(method))
            {
                GatherFamily(members, placed, ordered, method, i);
            }
        }

        return type.WithMembers(SyntaxFactory.List(ordered));
    }

    /// <summary>Appends the later overloads of one family directly after its first member.</summary>
    /// <param name="members">The type's members.</param>
    /// <param name="placed">The members already placed in the new order.</param>
    /// <param name="ordered">The new member order being built.</param>
    /// <param name="first">The family's first member.</param>
    /// <param name="firstIndex">The index of the family's first member.</param>
    private static void GatherFamily(
        SyntaxList<MemberDeclarationSyntax> members,
        bool[] placed,
        List<MemberDeclarationSyntax> ordered,
        MethodDeclarationSyntax first,
        int firstIndex)
    {
        for (var i = firstIndex + 1; i < members.Count; i++)
        {
            if (placed[i]
                || members[i] is not MethodDeclarationSyntax sibling
                || !Sst1218OverloadsGroupedAnalyzer.IsGroupable(sibling)
                || !Sst1218OverloadsGroupedAnalyzer.IsSameFamily(sibling, first))
            {
                continue;
            }

            ordered.Add(sibling);
            placed[i] = true;
        }
    }

    /// <summary>Resolves a diagnostic to the type whose overload families the batch regroups.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The type declaring the separated overload, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeDeclarationSyntax? FindRegroupableType(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetSeparatedOverload(root, diagnostic, out var type, out _, out _) ? type : null;

    /// <summary>Resolves a diagnostic to the overload it reported and the member that overload should follow.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="type">The type that declares the overload.</param>
    /// <param name="method">The out-of-place overload.</param>
    /// <param name="anchorIndex">The index of the overload it should follow.</param>
    /// <returns><see langword="true"/> when the reported shape still matches and can be moved safely.</returns>
    private static bool TryGetSeparatedOverload(
        SyntaxNode root,
        Diagnostic diagnostic,
        out TypeDeclarationSyntax? type,
        out MethodDeclarationSyntax? method,
        out int anchorIndex)
    {
        type = null;
        method = null;
        anchorIndex = -1;

        if (DiagnosticAncestor.Find<MethodDeclarationSyntax>(root, diagnostic.Location.SourceSpan) is not { } candidate
            || candidate.Parent is not TypeDeclarationSyntax declaringType
            || declaringType.ContainsDirectives)
        {
            return false;
        }

        var index = declaringType.Members.IndexOf(candidate);
        if (index < 0 || !Sst1218OverloadsGroupedAnalyzer.TryFindSeparatedOverload(declaringType.Members, index, out anchorIndex))
        {
            return false;
        }

        type = declaringType;
        method = candidate;
        return true;
    }

    /// <summary>Resolves the separated overload and builds its type with the overload moved beside its family.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the overload is no longer separated.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetSeparatedOverload(root, diagnostic, out var type, out var method, out var anchorIndex)
            ? new NodeReplacement(type!, type!.WithMembers(type.Members.Remove(method!).Insert(anchorIndex + 1, method!)))
            : null;
}
