// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace StyleSharp.Analyzers;

/// <summary>Reorders a declaration's <c>where</c> constraint clauses to match the type-parameter order (SST1221).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1221ConstraintClauseOrderCodeFixProvider))]
[Shared]
public sealed class Sst1221ConstraintClauseOrderCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(OrderingRules.ConstraintClauseOrder.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Order the constraint clauses by type parameter",
            nameof(Sst1221ConstraintClauseOrderCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported clause and reorders its declaration's constraints.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<TypeParameterConstraintClauseSyntax>() is not { Parent: { } declaration }
            || !GenericConstraintLayout.TryGet(declaration, out var typeParameters, out var clauses)
            || typeParameters is null
            || clauses.Count < 2)
        {
            return null;
        }

        var reordered = Reorder(typeParameters, clauses);
        return reordered is null ? null : new NodeReplacement(declaration, GenericConstraintLayout.WithConstraintClauses(declaration, reordered.Value));
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
            rebuilt[slot] = moved
                .WithLeadingTrivia(clauses[slot].GetLeadingTrivia())
                .WithTrailingTrivia(clauses[slot].GetTrailingTrivia());
        }

        return SyntaxFactory.List(rebuilt);
    }
}
