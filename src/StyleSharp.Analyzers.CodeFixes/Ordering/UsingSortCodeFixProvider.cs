// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Sorts a container's using directives into the canonical order (SST1208–SST1217).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UsingSortCodeFixProvider))]
[Shared]
public sealed class UsingSortCodeFixProvider : CodeFixProvider
{
    /// <summary>Compares using directives by the shared canonical ordering.</summary>
    private static readonly IComparer<UsingDirectiveSyntax> ComparerInstance = new UsingDirectiveComparer();

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

            // The sort reassigns each slot's trivia by position, which would scramble any
            // conditional compilation directives (#if/#elif/#else/#endif) living in the using
            // block. Those usings cannot be reordered across branches anyway, so don't offer the
            // fix when the block spans conditional directives — matching the member-ordering fix.
            if (UsingsSpanConditionalDirectives(Usings(container)))
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

    /// <summary>Sorts the container's using directives, keeping each slot's trivia.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="container">The container whose usings are sorted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> SortAsync(Document document, SyntaxNode container, CancellationToken cancellationToken)
    {
        var original = Usings(container);
        if (original.Count <= 1)
        {
            return document;
        }

        var ordered = new UsingDirectiveSyntax[original.Count];
        for (var i = 0; i < original.Count; i++)
        {
            ordered[i] = original[i];
        }

        Array.Sort(ordered, ComparerInstance);

        var rebuilt = new UsingDirectiveSyntax[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            rebuilt[index] = ordered[index]
                .WithLeadingTrivia(original[index].GetLeadingTrivia())
                .WithTrailingTrivia(original[index].GetTrailingTrivia());
        }

        var newContainer = WithUsings(container, SyntaxFactory.List(rebuilt));
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(container, newContainer));
    }

    /// <summary>Returns whether a using list spans conditional compilation directives.</summary>
    /// <param name="usings">The using directives to scan.</param>
    /// <returns><see langword="true"/> when an <c>#if</c>/<c>#elif</c>/<c>#else</c>/<c>#endif</c> lies within the block.</returns>
    private static bool UsingsSpanConditionalDirectives(SyntaxList<UsingDirectiveSyntax> usings)
    {
        for (var index = 0; index < usings.Count; index++)
        {
            var directive = usings[index];
            if (HasConditionalDirective(directive.GetLeadingTrivia()) || HasConditionalDirective(directive.GetTrailingTrivia()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a trivia list contains a conditional compilation directive.</summary>
    /// <param name="trivia">The trivia list to scan.</param>
    /// <returns><see langword="true"/> when a conditional directive is present.</returns>
    private static bool HasConditionalDirective(SyntaxTriviaList trivia)
    {
        for (var index = 0; index < trivia.Count; index++)
        {
            switch (trivia[index].Kind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                case SyntaxKind.ElifDirectiveTrivia:
                case SyntaxKind.ElseDirectiveTrivia:
                case SyntaxKind.EndIfDirectiveTrivia:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Returns the using list of a container that holds using directives.</summary>
    /// <param name="container">The container node.</param>
    /// <returns>The using directives.</returns>
    private static SyntaxList<UsingDirectiveSyntax> Usings(SyntaxNode container) => container switch
    {
        CompilationUnitSyntax unit => unit.Usings,
        NamespaceDeclarationSyntax ns => ns.Usings,
        FileScopedNamespaceDeclarationSyntax file => file.Usings,
        _ => default
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
        _ => container
    };

    /// <summary>Provides a reusable comparer instance for array sorting.</summary>
    private sealed class UsingDirectiveComparer : IComparer<UsingDirectiveSyntax>
    {
        /// <summary>Compares two using directives according to the canonical ordering rules.</summary>
        /// <param name="left">The left directive.</param>
        /// <param name="right">The right directive.</param>
        /// <returns>A negative, zero, or positive value according to canonical ordering.</returns>
        public int Compare(UsingDirectiveSyntax? left, UsingDirectiveSyntax? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            return UsingClassification.Compare(left, right);
        }
    }
}
