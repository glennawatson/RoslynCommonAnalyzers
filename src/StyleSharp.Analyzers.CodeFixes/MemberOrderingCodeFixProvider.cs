// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves an out-of-order member to its correct ordered position (SST1201–SST1215).
/// The member is relocated just after the last sibling of an equal-or-earlier rank,
/// carrying its own trivia. The fix is only offered when the file has no conditional
/// (<c>#if</c>/<c>#elif</c>/<c>#else</c>/<c>#endif</c>) directives, which could move
/// a member across a compilation boundary.
/// </summary>
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
        if (root is null || HasConditionalDirectives(root))
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var member = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MemberDeclarationSyntax>();
            if (member?.Parent is not TypeDeclarationSyntax)
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
    private static async Task<Document> MoveAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        var type = (TypeDeclarationSyntax)member.Parent!;
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

    /// <summary>Returns whether the syntax tree contains conditional compilation directives.</summary>
    /// <param name="root">The compilation unit root.</param>
    /// <returns><see langword="true"/> when an <c>#if</c>/<c>#elif</c>/<c>#else</c>/<c>#endif</c> directive is present.</returns>
    private static bool HasConditionalDirectives(SyntaxNode root)
    {
        if (!root.ContainsDirectives)
        {
            return false;
        }

        for (var directive = root.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            if (directive.Kind() is SyntaxKind.IfDirectiveTrivia
                or SyntaxKind.ElifDirectiveTrivia
                or SyntaxKind.ElseDirectiveTrivia
                or SyntaxKind.EndIfDirectiveTrivia)
            {
                return true;
            }
        }

        return false;
    }
}
