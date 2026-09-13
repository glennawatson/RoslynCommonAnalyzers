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
    /// <remarks>
    /// The fix re-sorts the whole using container, so every diagnostic in a file targets the same node;
    /// the per-node batch editor cannot compose that, so the batch fixer (which sorts once) is kept.
    /// </remarks>
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
            if (UsingsSpanDirectives(Usings(container)))
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

        // Each slot keeps the trivia it had, so a directive the sort left where it started already
        // carries the right trivia and needs no rewrite. Reattaching it anyway builds two new nodes
        // per directive - one for the leading trivia and one for the trailing - for every using in
        // the file, and a partly sorted list is the common case.
        for (var index = 0; index < ordered.Length; index++)
        {
            var directive = ordered[index];
            if (ReferenceEquals(directive, original[index]))
            {
                continue;
            }

            var globalKeyword = directive.GlobalKeyword;
            var usingKeyword = directive.UsingKeyword;
            if (globalKeyword.RawKind != 0)
            {
                globalKeyword = globalKeyword.WithLeadingTrivia(original[index].GetLeadingTrivia());
            }
            else
            {
                usingKeyword = usingKeyword.WithLeadingTrivia(original[index].GetLeadingTrivia());
            }

            ordered[index] = directive.Update(
                globalKeyword,
                usingKeyword,
                directive.StaticKeyword,
                directive.UnsafeKeyword,
                directive.Alias,
                directive.NamespaceOrType,
                directive.SemicolonToken.WithTrailingTrivia(original[index].GetTrailingTrivia()));
        }

        var newContainer = WithUsings(container, SyntaxFactory.List(ordered));
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(container, newContainer));
    }

    /// <summary>Returns whether a directive stands among the using directives.</summary>
    /// <param name="usings">The using directives to scan.</param>
    /// <returns><see langword="true"/> when any directive lies within the block.</returns>
    /// <remarks>
    /// Sorting reattaches each slot's trivia to whichever directive lands there, so a directive among them
    /// ends up introducing a different using than the one it was written above. A <c>#region</c> reads as
    /// covering the wrong imports, and an <c>#if</c> compiles the wrong ones.
    /// </remarks>
    private static bool UsingsSpanDirectives(SyntaxList<UsingDirectiveSyntax> usings)
    {
        for (var index = 0; index < usings.Count; index++)
        {
            var directive = usings[index];
            if (HasDirective(directive.GetLeadingTrivia()) || HasDirective(directive.GetTrailingTrivia()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a trivia list contains a directive.</summary>
    /// <param name="trivia">The trivia list to scan.</param>
    /// <returns><see langword="true"/> when a directive is present.</returns>
    private static bool HasDirective(in SyntaxTriviaList trivia)
    {
        for (var index = 0; index < trivia.Count; index++)
        {
            if (trivia[index].IsDirective)
            {
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
        /// <param name="x">The left directive.</param>
        /// <param name="y">The right directive.</param>
        /// <returns>A negative, zero, or positive value according to canonical ordering.</returns>
        public int Compare(UsingDirectiveSyntax? x, UsingDirectiveSyntax? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            return y is null ? 1 : UsingClassification.Compare(x, y);
        }
    }
}
