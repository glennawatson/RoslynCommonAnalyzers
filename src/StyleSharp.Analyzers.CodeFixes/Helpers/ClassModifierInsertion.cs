// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Inserts a modifier into a class declaration after its access modifiers and ahead of <c>partial</c>, which stays last.</summary>
internal static class ClassModifierInsertion
{
    /// <summary>Inserts a modifier keyword into a class declaration.</summary>
    /// <param name="declaration">The class declaration.</param>
    /// <param name="kind">The modifier keyword to insert.</param>
    /// <param name="takePartialIndentation">
    /// Whether the inserted modifier takes over the leading trivia of the <c>partial</c> it lands in front of. When
    /// <c>partial</c> leads the list that trivia is the declaration's own indentation.
    /// </param>
    /// <returns>The class declaration carrying the modifier.</returns>
    internal static ClassDeclarationSyntax InsertBeforePartial(ClassDeclarationSyntax declaration, SyntaxKind kind, bool takePartialIndentation)
    {
        var modifiers = declaration.Modifiers;
        if (modifiers.Count == 0)
        {
            // No modifiers: move the declaration's leading trivia onto the modifier and re-indent the keyword.
            var lone = SyntaxFactory.Token(declaration.GetLeadingTrivia(), kind, SyntaxFactory.TriviaList(SyntaxFactory.Space));
            return (ClassDeclarationSyntax)TypeDeclarationRewrite.WithHead(
                declaration,
                SyntaxFactory.TokenList(lone),
                declaration.Keyword.WithLeadingTrivia(SyntaxFactory.TriviaList()))!;
        }

        var partialIndex = modifiers.IndexOf(SyntaxKind.PartialKeyword);
        if (partialIndex < 0 || !takePartialIndentation)
        {
            var inserted = SyntaxFactory.Token(default, kind, SyntaxFactory.TriviaList(SyntaxFactory.Space));
            return declaration.WithModifiers(partialIndex < 0 ? modifiers.Add(inserted) : modifiers.Insert(partialIndex, inserted));
        }

        var partial = modifiers[partialIndex];
        var leading = SyntaxFactory.Token(partial.LeadingTrivia, kind, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        var reindented = modifiers.Replace(partial, partial.WithLeadingTrivia(SyntaxFactory.TriviaList()));
        return declaration.WithModifiers(reindented.Insert(partialIndex, leading));
    }
}
