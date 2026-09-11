// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves an out-of-order member to its correct ordered position (SST1201–SST1215).
/// The member is relocated just after the last sibling of an equal-or-earlier rank,
/// carrying its own trivia.
/// </summary>
/// <remarks>
/// A directive belongs to a position in the file, not to the member it sits above, so a move
/// carries whichever half of a pair the member's trivia happens to hold and leaves the other
/// behind. An <c>#endregion</c> in the moved member's leading trivia lands above its own
/// <c>#region</c>, which is CS1028; a member leaving an <c>#if</c> stops compiling on the
/// configurations that defined it; a member leaving a <c>#pragma warning disable</c> pair
/// silently starts warning again. None of that is visible in the member being moved, so the fix
/// is offered only for a type free of directives, in a file free of conditional ones.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberOrderingCodeFixProvider))]
[Shared]
public sealed class MemberOrderingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        OrderingRules.OrderByKind.Id,
        OrderingRules.OrderByAccess.Id,
        OrderingRules.ConstantsBeforeFields.Id,
        OrderingRules.StaticBeforeInstance.Id,
        OrderingRules.ReadonlyBeforeNonReadonly.Id,
        OrderingRules.InstanceReadonlyBeforeNonReadonly.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || DirectiveBoundaries.AnyConditional(root))
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var member = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MemberDeclarationSyntax>();
            if (member?.Parent is not TypeDeclarationSyntax type || DirectiveBoundaries.SeparateMembers(type))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Move to ordered position",
                    cancellationToken => MoveAsync(context.Document, member, cancellationToken),
                    equivalenceKey: nameof(MemberOrderingCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Relocates <paramref name="member"/> to its ordered position within its containing type.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="member">The out-of-order member.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> MoveAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        var type = (TypeDeclarationSyntax)member.Parent!;
        if (DirectiveBoundaries.SeparateMembers(type))
        {
            return document;
        }

        var members = type.Members;
        var flaggedIndex = members.IndexOf(member);

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var marker = model is null ? null : MemberOrder.ResolveUnionMarker(model.Compilation);

        if (RankOf(member, model, marker, cancellationToken) is not { } order)
        {
            return document;
        }

        var target = TargetIndex(members, flaggedIndex, order, model, marker, cancellationToken);
        if (target == flaggedIndex)
        {
            return document;
        }

        var reordered = ReorderMembers(members, flaggedIndex, target);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(type, type.WithMembers(reordered));
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>Rebuilds the member list with one member moved to a new index.</summary>
    /// <param name="members">The original members.</param>
    /// <param name="sourceIndex">The member's current index.</param>
    /// <param name="targetIndex">The index to move the member to.</param>
    /// <returns>The reordered syntax list.</returns>
    private static SyntaxList<MemberDeclarationSyntax> ReorderMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        int sourceIndex,
        int targetIndex)
    {
        var reordered = new MemberDeclarationSyntax[members.Count];
        var moved = members[sourceIndex];
        var destination = 0;

        for (var index = 0; index < members.Count; index++)
        {
            if (destination == targetIndex)
            {
                reordered[destination] = moved;
                destination++;
            }

            if (index == sourceIndex)
            {
                continue;
            }

            reordered[destination] = members[index];
            destination++;
        }

        if (destination == targetIndex)
        {
            reordered[destination] = moved;
        }

        return SyntaxFactory.List(reordered);
    }

    /// <summary>Finds the first position before the flagged member that the member should sort ahead of.</summary>
    /// <param name="members">The containing type's members.</param>
    /// <param name="flaggedIndex">The flagged member's current index.</param>
    /// <param name="order">The flagged member's rank.</param>
    /// <param name="model">The semantic model, or <see langword="null"/>.</param>
    /// <param name="marker">The resolved <c>IUnion</c> marker, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The index at which to insert the member.</returns>
    private static int TargetIndex(
        SyntaxList<MemberDeclarationSyntax> members,
        int flaggedIndex,
        MemberOrder order,
        SemanticModel? model,
        INamedTypeSymbol? marker,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < flaggedIndex; index++)
        {
            if (RankOf(members[index], model, marker, cancellationToken) is { } rank && rank.CompareTo(order) > 0)
            {
                return index;
            }
        }

        return flaggedIndex;
    }

    /// <summary>Classifies a member's rank, detecting unions when the marker is available.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="model">The semantic model, or <see langword="null"/>.</param>
    /// <param name="marker">The resolved <c>IUnion</c> marker, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The rank, or <see langword="null"/> when the member is not ordered.</returns>
    private static MemberOrder? RankOf(MemberDeclarationSyntax member, SemanticModel? model, INamedTypeSymbol? marker, CancellationToken cancellationToken)
    {
        var isUnion = model is not null && marker is not null
            && MemberOrder.IsUnion(member, model, marker, cancellationToken);
        return MemberOrder.Classify(member, isUnion);
    }
}
