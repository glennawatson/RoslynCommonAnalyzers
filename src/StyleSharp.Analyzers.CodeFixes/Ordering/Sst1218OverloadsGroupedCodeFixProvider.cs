// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
public sealed class Sst1218OverloadsGroupedCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(OrderingRules.OverloadsGrouped.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetSeparatedOverload(root, diagnostic, out var type, out var method, out var anchorIndex))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Move the overload beside its family",
                    _ => Task.FromResult(Apply(context.Document, root, type!, method!, anchorIndex)),
                    equivalenceKey: nameof(Sst1218OverloadsGroupedCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetSeparatedOverload(editor.OriginalRoot, diagnostic, out var type, out _, out _))
        {
            return;
        }

        // Every diagnostic in one type asks for the same end state, and regrouping is idempotent, so the
        // batch can compose the requests instead of trying to sequence a series of single-member moves.
        editor.ReplaceNode(type!, (current, _) => Regroup(current));
    }

    /// <summary>Moves one overload to sit immediately after the nearest earlier member of its family.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="type">The type that declares the overload.</param>
    /// <param name="method">The out-of-place overload.</param>
    /// <param name="anchorIndex">The index of the overload it should follow.</param>
    /// <returns>The updated document.</returns>
    private static Document Apply(
        Document document,
        SyntaxNode root,
        TypeDeclarationSyntax type,
        MethodDeclarationSyntax method,
        int anchorIndex)
    {
        var members = type.Members.Remove(method).Insert(anchorIndex + 1, method);
        return document.WithSyntaxRoot(root.ReplaceNode(type, type.WithMembers(members)));
    }

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

        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } candidate
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
}
