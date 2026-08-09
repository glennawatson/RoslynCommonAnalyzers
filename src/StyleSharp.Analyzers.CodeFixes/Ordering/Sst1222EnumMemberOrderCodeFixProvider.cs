// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.CodeActions;

namespace StyleSharp.Analyzers;

/// <summary>
/// Sorts an enum's members into ascending value order (SST1222). Each member moves with the text it owns, so
/// the fix is offered only when no member carries a comment or documentation that reordering would separate
/// from what it describes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1222EnumMemberOrderCodeFixProvider))]
[Shared]
public sealed class Sst1222EnumMemberOrderCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(OrderingRules.EnumMemberOrder.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<EnumDeclarationSyntax>() is not { } declaration
                || Sst1222EnumMemberOrderAnalyzer.TryGetExplicitValues(declaration, model, context.CancellationToken) is not { } values
                || CarriesComments(declaration))
            {
                continue;
            }

            var sorted = Sort(declaration, values);
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Sort the enum members by value",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, sorted))),
                    nameof(Sst1222EnumMemberOrderCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Returns whether any member carries trivia that must stay with what it describes.</summary>
    /// <param name="declaration">The enum declaration.</param>
    /// <returns><see langword="true"/> when a member has a comment or documentation attached.</returns>
    private static bool CarriesComments(EnumDeclarationSyntax declaration)
    {
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            foreach (var trivia in members[i].GetLeadingTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Rebuilds the enum with its members in ascending value order.</summary>
    /// <param name="declaration">The enum declaration.</param>
    /// <param name="values">The members' values, in declaration order.</param>
    /// <returns>The sorted enum declaration.</returns>
    /// <remarks>
    /// The members are permuted while the separators and each position's own trivia stay put, so the sorted
    /// enum keeps the original's indentation and trailing-comma shape rather than being reformatted.
    /// </remarks>
    private static EnumDeclarationSyntax Sort(EnumDeclarationSyntax declaration, long[] values)
    {
        var members = declaration.Members;
        var order = new int[members.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort((long[])values.Clone(), order);

        var sorted = new List<EnumMemberDeclarationSyntax>(members.Count);
        for (var i = 0; i < order.Length; i++)
        {
            sorted.Add(members[order[i]]
                .WithLeadingTrivia(members[i].GetLeadingTrivia())
                .WithTrailingTrivia(members[i].GetTrailingTrivia()));
        }

        var separators = new List<SyntaxToken>(members.SeparatorCount);
        for (var i = 0; i < members.SeparatorCount; i++)
        {
            separators.Add(members.GetSeparator(i));
        }

        return declaration.WithMembers(SyntaxFactory.SeparatedList(sorted, separators));
    }
}
