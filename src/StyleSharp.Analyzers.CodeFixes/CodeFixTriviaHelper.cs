// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Provides trivia transformations shared by code fixes.</summary>
internal static class CodeFixTriviaHelper
{
    /// <summary>Removes one blank line from the start of a member's leading trivia.</summary>
    /// <param name="trivia">The member's leading trivia.</param>
    /// <returns>The leading trivia with at most one initial line break.</returns>
    internal static SyntaxTriviaList CollapseLeadingBlankLine(SyntaxTriviaList trivia)
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

    /// <summary>Finds the single annotated property declaration.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="annotation">The annotation to look up.</param>
    /// <returns>The annotated property declaration.</returns>
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
}
