// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Sorts a container's using directives into the canonical order (SST1208–SST1217).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UsingSortCodeFixProvider))]
[Shared]
public sealed class UsingSortCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        OrderingRules.SystemUsingsFirst.Id,
        OrderingRules.AliasUsingsLast.Id,
        OrderingRules.RegularUsingsAlphabetical.Id,
        OrderingRules.AliasUsingsAlphabetical.Id,
        OrderingRules.StaticUsingsPlacement.Id,
        OrderingRules.StaticUsingsAlphabetical.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<UsingDirectiveSyntax>()?.Parent is not { } container)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Sort using directives",
                    cancellationToken => SortAsync(context.Document, container, cancellationToken),
                    equivalenceKey: nameof(UsingSortCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Returns the using list of a container that holds using directives.</summary>
    /// <param name="container">The container node.</param>
    /// <returns>The using directives.</returns>
    private static SyntaxList<UsingDirectiveSyntax> Usings(SyntaxNode container) => container switch
    {
        CompilationUnitSyntax unit => unit.Usings,
        NamespaceDeclarationSyntax ns => ns.Usings,
        FileScopedNamespaceDeclarationSyntax file => file.Usings,
        _ => default,
    };

    /// <summary>Replaces a container's using list with the canonically sorted equivalent.</summary>
    /// <param name="container">The container node.</param>
    /// <param name="usings">The sorted using list.</param>
    /// <returns>The updated container.</returns>
    private static SyntaxNode WithUsings(SyntaxNode container, SyntaxList<UsingDirectiveSyntax> usings) => container switch
    {
        CompilationUnitSyntax unit => unit.WithUsings(usings),
        NamespaceDeclarationSyntax ns => ns.WithUsings(usings),
        FileScopedNamespaceDeclarationSyntax file => file.WithUsings(usings),
        _ => container,
    };

    /// <summary>Sorts the container's using directives, keeping each slot's trivia.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="container">The container whose usings are sorted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> SortAsync(Document document, SyntaxNode container, CancellationToken cancellationToken)
    {
        var original = Usings(container);
        var ordered = original.OrderBy(directive => directive, Comparer<UsingDirectiveSyntax>.Create(UsingClassification.Compare)).ToList();

        var rebuilt = new List<UsingDirectiveSyntax>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            rebuilt.Add(ordered[index]
                .WithLeadingTrivia(original[index].GetLeadingTrivia())
                .WithTrailingTrivia(original[index].GetTrailingTrivia()));
        }

        var newContainer = WithUsings(container, SyntaxFactory.List(rebuilt));
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(container, newContainer));
    }
}
