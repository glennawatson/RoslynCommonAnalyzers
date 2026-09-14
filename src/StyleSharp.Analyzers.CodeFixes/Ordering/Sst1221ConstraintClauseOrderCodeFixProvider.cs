// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace StyleSharp.Analyzers;

/// <summary>Reorders a declaration's <c>where</c> constraint clauses to match the type-parameter order (SST1221).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1221ConstraintClauseOrderCodeFixProvider))]
[Shared]
public sealed class Sst1221ConstraintClauseOrderCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(OrderingRules.ConstraintClauseOrder.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Order the constraint clauses by type parameter",
            nameof(Sst1221ConstraintClauseOrderCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryResolve(root, diagnostic, out _, out var typeParameters, out var clauses))
        {
            return false;
        }

        foreach (var clause in clauses)
        {
            if (GenericConstraintLayout.PositionOf(typeParameters, clause.Name.Identifier.ValueText) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Resolves the reported clause and reorders its declaration's constraints.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryResolve(root, diagnostic, out var declaration, out var typeParameters, out var clauses))
        {
            return null;
        }

        var reordered = Reorder(typeParameters, clauses);
        return reordered is null ? null : new NodeReplacement(declaration, GenericConstraintLayout.WithConstraintClauses(declaration, reordered.Value));
    }

    /// <summary>Resolves the declaration owning the reported clause, with its type parameters and constraint clauses.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="declaration">The generic declaration.</param>
    /// <param name="typeParameters">The declaration's type-parameter list.</param>
    /// <param name="clauses">The declaration's constraint clauses.</param>
    /// <returns><see langword="true"/> when the declaration is generic and carries at least two clauses.</returns>
    private static bool TryResolve(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out SyntaxNode? declaration,
        [NotNullWhen(true)] out TypeParameterListSyntax? typeParameters,
        out SyntaxList<TypeParameterConstraintClauseSyntax> clauses)
    {
        declaration = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<TypeParameterConstraintClauseSyntax>()?.Parent;
        if (declaration is null || !GenericConstraintLayout.TryGet(declaration, out typeParameters, out clauses))
        {
            typeParameters = null;
            clauses = default;
            return false;
        }

        return typeParameters is not null && clauses.Count >= 2;
    }

    /// <summary>Rebuilds the constraint clauses in type-parameter order, keeping each slot's trivia.</summary>
    /// <param name="typeParameters">The declaration's type-parameter list.</param>
    /// <param name="clauses">The declaration's constraint clauses.</param>
    /// <returns>The reordered clauses, or <see langword="null"/> when a clause names an unknown type parameter.</returns>
    private static SyntaxList<TypeParameterConstraintClauseSyntax>? Reorder(
        TypeParameterListSyntax typeParameters,
        SyntaxList<TypeParameterConstraintClauseSyntax> clauses)
    {
        var count = clauses.Count;
        var positions = new int[count];
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            var position = GenericConstraintLayout.PositionOf(typeParameters, clauses[i].Name.Identifier.ValueText);
            if (position < 0)
            {
                return null;
            }

            positions[i] = position;
            order[i] = i;
        }

        Array.Sort(order, (left, right) => positions[left] - positions[right]);

        var rebuilt = new TypeParameterConstraintClauseSyntax[count];
        for (var slot = 0; slot < count; slot++)
        {
            var moved = clauses[order[slot]];
            var constraints = moved.Constraints;
            var colonToken = moved.ColonToken;
            if (constraints.Count > 0)
            {
                var last = constraints[constraints.Count - 1];
                constraints = constraints.Replace(last, last.WithTrailingTrivia(clauses[slot].GetTrailingTrivia()));
            }
            else
            {
                colonToken = colonToken.WithTrailingTrivia(clauses[slot].GetTrailingTrivia());
            }

            rebuilt[slot] = moved.Update(
                moved.WhereKeyword.WithLeadingTrivia(clauses[slot].GetLeadingTrivia()),
                moved.Name,
                colonToken,
                constraints);
        }

        return SyntaxFactory.List(rebuilt);
    }
}
