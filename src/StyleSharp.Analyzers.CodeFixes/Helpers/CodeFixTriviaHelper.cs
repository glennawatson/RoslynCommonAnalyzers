// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Provides trivia transformations shared by code fixes.</summary>
internal static class CodeFixTriviaHelper
{
    /// <summary>Removes one blank line from the start of a member's leading trivia.</summary>
    /// <param name="trivia">The member's leading trivia.</param>
    /// <returns>The leading trivia with at most one initial line break.</returns>
    internal static SyntaxTriviaList CollapseLeadingBlankLine(in SyntaxTriviaList trivia)
    {
        var firstEndOfLine = -1;
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (firstEndOfLine >= 0)
                {
                    return trivia.RemoveAt(firstEndOfLine);
                }

                firstEndOfLine = i;
                continue;
            }

            if (!trivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                break;
            }
        }

        return trivia;
    }

    /// <summary>Gets the indentation a declaration starts at, from its leading trivia.</summary>
    /// <param name="leading">The declaration's leading trivia.</param>
    /// <returns>The whitespace immediately before the declaration, or an empty list when it starts at column zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SyntaxTriviaList IndentTrivia(in SyntaxTriviaList leading) =>
        leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : SyntaxTriviaList.Empty;

    /// <summary>Finds the single annotated property declaration.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="annotation">The annotation to look up.</param>
    /// <returns>The annotated property declaration.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="root"/> carries no property declaration marked with <paramref name="annotation"/>, or carries more than one.</exception>
    internal static PropertyDeclarationSyntax GetSingleAnnotatedProperty(SyntaxNode root, SyntaxAnnotation annotation)
    {
        PropertyDeclarationSyntax? result = null;
        var found = false;
        foreach (var node in root.GetAnnotatedNodes(annotation))
        {
            if (node is not PropertyDeclarationSyntax property)
            {
                continue;
            }

            if (found)
            {
                throw new InvalidOperationException("Expected a single annotated node.");
            }

            result = property;
            found = true;
        }

        return found ? result! : throw new InvalidOperationException("Annotated node not found.");
    }

    /// <summary>Swaps a property for its rewritten form and deletes the backing field it no longer needs.</summary>
    /// <param name="root">The syntax root holding both declarations.</param>
    /// <param name="property">The property as it stands in <paramref name="root"/>.</param>
    /// <param name="updated">The rewritten property.</param>
    /// <param name="field">The backing-field declaration to delete.</param>
    /// <returns>The updated root.</returns>
    /// <remarks>
    /// The field goes with none of its trivia, so the separation the property had from whatever preceded the
    /// field is rebuilt from the token before it: its trailing trivia moves onto the property and one blank
    /// line of the combined run is dropped.
    /// </remarks>
    internal static SyntaxNode ReplacePropertyRemovingField(
        SyntaxNode root,
        PropertyDeclarationSyntax property,
        PropertyDeclarationSyntax updated,
        FieldDeclarationSyntax field)
    {
        var annotation = new SyntaxAnnotation();
        updated = updated.WithAdditionalAnnotations(annotation);
        var changed = root.TrackNodes(property, field);
        var trackedProperty = changed.GetCurrentNode(property)!;
        changed = changed.ReplaceNode(trackedProperty, updated);
        var trackedField = changed.GetCurrentNode(field)!;
        changed = changed.RemoveNode(trackedField, SyntaxRemoveOptions.KeepNoTrivia)!;
        var currentProperty = GetSingleAnnotatedProperty(changed, annotation);
        var previousToken = currentProperty.GetFirstToken().GetPreviousToken();
        var leadingTrivia = previousToken.TrailingTrivia.AddRange(currentProperty.GetLeadingTrivia());
        changed = changed.ReplaceToken(previousToken, previousToken.WithTrailingTrivia(default(SyntaxTriviaList)));
        currentProperty = GetSingleAnnotatedProperty(changed, annotation);
        var normalizedProperty = currentProperty.WithLeadingTrivia(CollapseLeadingBlankLine(leadingTrivia));
        return changed.ReplaceNode(currentProperty, normalizedProperty);
    }
}
