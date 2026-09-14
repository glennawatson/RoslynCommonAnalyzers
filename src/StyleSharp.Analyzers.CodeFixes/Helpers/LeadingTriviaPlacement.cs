// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Places leading trivia on whichever token opens a declaration rebuilt from its parts.</summary>
/// <remarks>
/// A declaration that is moved into another slot takes that slot's leading trivia. The trivia belongs on the first
/// attribute list when there is one, otherwise on the first modifier, otherwise on the keyword or name.
/// </remarks>
internal static class LeadingTriviaPlacement
{
    /// <summary>Puts the trivia on the first token of a declaration's attribute lists, modifiers and leading token.</summary>
    /// <param name="attributeLists">The declaration's attribute lists; the first is updated when present.</param>
    /// <param name="modifiers">The declaration's modifiers; the first is updated when there are no attribute lists.</param>
    /// <param name="firstToken">The keyword or name that follows the modifiers; updated when there is nothing before it.</param>
    /// <param name="leadingTrivia">The trivia to place.</param>
    internal static void PlaceOnFirstToken(
        ref SyntaxList<AttributeListSyntax> attributeLists,
        ref SyntaxTokenList modifiers,
        ref SyntaxToken firstToken,
        in SyntaxTriviaList leadingTrivia)
    {
        if (attributeLists.Count > 0)
        {
            attributeLists = attributeLists.Replace(attributeLists[0], attributeLists[0].WithLeadingTrivia(leadingTrivia));
        }
        else if (modifiers.Count > 0)
        {
            modifiers = modifiers.Replace(modifiers[0], modifiers[0].WithLeadingTrivia(leadingTrivia));
        }
        else
        {
            firstToken = firstToken.WithLeadingTrivia(leadingTrivia);
        }
    }
}
