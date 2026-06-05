// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Allocation-free navigation over XML documentation comments. Everything works
/// directly on the parsed trivia (no strings are materialized) so a member's
/// documentation can be checked once, cheaply, in a single pass.
/// </summary>
internal static class XmlDocumentationHelper
{
    /// <summary>Returns the documentation comment attached to <paramref name="member"/>, or <see langword="null"/>.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The documentation comment trivia, or <see langword="null"/>.</returns>
    public static DocumentationCommentTriviaSyntax? GetDocumentationComment(SyntaxNode member)
    {
        foreach (var trivia in member.GetLeadingTrivia())
        {
            if ((trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                && trivia.GetStructure() is DocumentationCommentTriviaSyntax documentation)
            {
                return documentation;
            }
        }

        return null;
    }

    /// <summary>Returns the first top-level element named <paramref name="name"/>, or <see langword="null"/>.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="name">The element name (e.g. <c>summary</c>).</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    public static XmlNodeSyntax? FindElement(DocumentationCommentTriviaSyntax documentation, string name)
    {
        foreach (var node in documentation.Content)
        {
            if (GetElementName(node) == name)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>Returns whether the documentation contains a top-level element named <paramref name="name"/>.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="name">The element name.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public static bool HasElement(DocumentationCommentTriviaSyntax documentation, string name)
        => FindElement(documentation, name) is not null;

    /// <summary>Returns whether the documentation uses <c>&lt;inheritdoc&gt;</c> (so content rules are skipped).</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <returns><see langword="true"/> when an inheritdoc element is present.</returns>
    public static bool IsInheritDoc(DocumentationCommentTriviaSyntax documentation)
        => HasElement(documentation, "inheritdoc");

    /// <summary>Returns whether a <c>&lt;param name="..."&gt;</c> element documents <paramref name="parameterName"/>.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns><see langword="true"/> when documented.</returns>
    public static bool IsParameterDocumented(DocumentationCommentTriviaSyntax documentation, string parameterName)
        => HasNamedElement(documentation, "param", parameterName);

    /// <summary>Returns whether a <c>&lt;typeparam name="..."&gt;</c> element documents <paramref name="typeParameterName"/>.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="typeParameterName">The type parameter name.</param>
    /// <returns><see langword="true"/> when documented.</returns>
    public static bool IsTypeParameterDocumented(DocumentationCommentTriviaSyntax documentation, string typeParameterName)
        => HasNamedElement(documentation, "typeparam", typeParameterName);

    /// <summary>Returns whether an element contains any non-whitespace text.</summary>
    /// <param name="node">The element node.</param>
    /// <returns><see langword="true"/> when non-whitespace text is present.</returns>
    public static bool HasText(XmlNodeSyntax node)
    {
        if (node is not XmlElementSyntax element)
        {
            return false;
        }

        foreach (var token in element.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
            {
                continue;
            }

            foreach (var character in token.ValueText)
            {
                if (!char.IsWhiteSpace(character))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether an element's prose should gain a terminal period — i.e.
    /// its last significant content is plain text not already ending in terminal
    /// punctuation. Skips elements whose content ends with an inline element (e.g.
    /// <c>&lt;see/&gt;</c>) to avoid false positives.
    /// </summary>
    /// <param name="element">The prose element (summary, returns, …).</param>
    /// <param name="insertPosition">Where a period should be inserted when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a terminal period is missing.</returns>
    public static bool NeedsTerminalPeriod(XmlElementSyntax element, out int insertPosition)
    {
        insertPosition = -1;

        XmlNodeSyntax? lastSignificant = null;
        foreach (var node in element.Content)
        {
            if (node is XmlTextSyntax text)
            {
                if (ContainsNonWhitespace(text))
                {
                    lastSignificant = text;
                }
            }
            else
            {
                lastSignificant = node;
            }
        }

        if (lastSignificant is not XmlTextSyntax lastText
            || !TryGetLastTextCharacter(lastText, out var character, out var position)
            || IsTerminalPunctuation(character))
        {
            return false;
        }

        insertPosition = position + 1;
        return true;
    }

    /// <summary>Finds the last non-whitespace text character of a node and its absolute position.</summary>
    /// <param name="node">The node to scan.</param>
    /// <param name="character">The last non-whitespace character when found.</param>
    /// <param name="position">The absolute source position of that character when found.</param>
    /// <returns><see langword="true"/> when the node has text.</returns>
    public static bool TryGetLastTextCharacter(XmlNodeSyntax node, out char character, out int position)
    {
        character = '\0';
        position = -1;

        foreach (var token in node.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
            {
                continue;
            }

            var text = token.ValueText;
            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    character = text[i];

                    // ValueText positions line up with the token span for XML text literals.
                    position = token.SpanStart + i;
                }
            }
        }

        return position >= 0;
    }

    /// <summary>Returns the local name of an XML element or empty element, or <see langword="null"/> for other nodes.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The element's local name, or <see langword="null"/>.</returns>
    public static string? GetElementName(XmlNodeSyntax node) => node switch
    {
        XmlElementSyntax element => element.StartTag.Name.LocalName.ValueText,
        XmlEmptyElementSyntax element => element.Name.LocalName.ValueText,
        _ => null,
    };

    /// <summary>Returns whether a character is terminal sentence punctuation.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for terminal punctuation.</returns>
    private static bool IsTerminalPunctuation(char character)
        => character is '.' or '!' or '?' or ':' or ';';

    /// <summary>Returns whether a node contains any non-whitespace text.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> when non-whitespace text is present.</returns>
    private static bool ContainsNonWhitespace(XmlNodeSyntax node)
    {
        foreach (var token in node.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
            {
                continue;
            }

            foreach (var character in token.ValueText)
            {
                if (!char.IsWhiteSpace(character))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a named element (e.g. <c>param</c>) with the given name attribute exists.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="elementName">The element name.</param>
    /// <param name="nameAttribute">The required <c>name</c> attribute value.</param>
    /// <returns><see langword="true"/> when a matching element is present.</returns>
    private static bool HasNamedElement(DocumentationCommentTriviaSyntax documentation, string elementName, string nameAttribute)
    {
        foreach (var node in documentation.Content)
        {
            if (GetElementName(node) == elementName && GetNameAttribute(node) == nameAttribute)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the value of a node's <c>name</c> attribute, or <see langword="null"/>.</summary>
    /// <param name="node">The element node.</param>
    /// <returns>The name attribute value, or <see langword="null"/>.</returns>
    private static string? GetNameAttribute(XmlNodeSyntax node)
    {
        var attributes = node switch
        {
            XmlElementSyntax element => element.StartTag.Attributes,
            XmlEmptyElementSyntax element => element.Attributes,
            _ => default,
        };

        foreach (var attribute in attributes)
        {
            if (attribute is XmlNameAttributeSyntax nameAttribute)
            {
                return nameAttribute.Identifier.Identifier.ValueText;
            }
        }

        return null;
    }
}
