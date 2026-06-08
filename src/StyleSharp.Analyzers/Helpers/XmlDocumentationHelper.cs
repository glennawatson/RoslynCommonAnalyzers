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

    /// <summary>Returns whether an element contains a nested <c>&lt;inheritdoc&gt;</c> (so its content is inherited).</summary>
    /// <param name="element">The element to scan.</param>
    /// <returns><see langword="true"/> when an inheritdoc descendant is present.</returns>
    public static bool ContainsInheritDoc(XmlNodeSyntax element)
    {
        var found = false;
        DescendantTraversalHelper.VisitDescendants<XmlNodeSyntax, bool>(element, ref found, VisitInheritDocNode);
        return found;
    }

    /// <summary>Returns the <c>&lt;param name="..."&gt;</c> element documenting <paramref name="parameterName"/>, or <see langword="null"/>.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The matching element, or <see langword="null"/>.</returns>
    public static XmlNodeSyntax? FindParameterElement(DocumentationCommentTriviaSyntax documentation, string parameterName)
        => FindNamedElement(documentation, "param", parameterName);

    /// <summary>Returns the <c>&lt;typeparam name="..."&gt;</c> element documenting <paramref name="typeParameterName"/>, or <see langword="null"/>.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="typeParameterName">The type parameter name.</param>
    /// <returns>The matching element, or <see langword="null"/>.</returns>
    public static XmlNodeSyntax? FindTypeParameterElement(DocumentationCommentTriviaSyntax documentation, string typeParameterName)
        => FindNamedElement(documentation, "typeparam", typeParameterName);

    /// <summary>Returns the first element named <paramref name="elementName"/> whose name attribute equals <paramref name="nameAttribute"/>.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="elementName">The element name (e.g. <c>param</c>).</param>
    /// <param name="nameAttribute">The required name attribute value.</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    public static XmlNodeSyntax? FindNamedElement(DocumentationCommentTriviaSyntax documentation, string elementName, string nameAttribute)
    {
        foreach (var node in documentation.Content)
        {
            if (GetElementName(node) == elementName && NameAttribute(node) == nameAttribute)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>Returns the value of a node's <c>name</c> attribute, or <see langword="null"/>.</summary>
    /// <param name="node">The element node.</param>
    /// <returns>The name attribute value, or <see langword="null"/>.</returns>
    public static string? NameAttribute(XmlNodeSyntax node)
    {
        var attributes = node switch
        {
            XmlElementSyntax element => element.StartTag.Attributes,
            XmlEmptyElementSyntax element => element.Attributes,
            _ => default
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

    /// <summary>Returns the element's text content with runs of whitespace collapsed to single spaces and trimmed.</summary>
    /// <param name="node">The element node.</param>
    /// <returns>The normalized text, or an empty string when there is none.</returns>
    public static string NormalizedText(XmlNodeSyntax node)
    {
        if (node is not XmlElementSyntax element)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        var pendingSpace = false;
        foreach (var token in element.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
            {
                continue;
            }

            foreach (var character in token.ValueText)
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(character);
            }
        }

        return builder.ToString();
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

        // The element ends with an inline element (e.g. <see/>): no trailing period expected.
        if (LastSignificantContent(element) is not XmlTextSyntax lastText
            || !TryGetTrailingCharacters(lastText, out var last, out var secondLast, out var position))
        {
            return false;
        }

        // Accept terminal punctuation, optionally tucked inside a closing quote/paren ('text."', '(text.)').
        if (IsTerminalPunctuation(last) || (IsClosingDelimiter(last) && IsTerminalPunctuation(secondLast)))
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

    /// <summary>
    /// Returns whether an element's leading prose begins with <paramref name="expected"/>.
    /// The comparison runs over the existing <c>ValueText</c> via a span (no
    /// substring is allocated); an element whose first significant content is an
    /// inline element rather than text returns <see langword="false"/>.
    /// </summary>
    /// <param name="element">The prose element.</param>
    /// <param name="expected">The expected leading text.</param>
    /// <returns><see langword="true"/> when the leading text matches.</returns>
    public static bool LeadingTextStartsWith(XmlElementSyntax element, ReadOnlySpan<char> expected)
    {
        foreach (var node in element.Content)
        {
            if (node is not XmlTextSyntax)
            {
                // First significant content is an inline element, not text.
                return false;
            }

            foreach (var token in node.DescendantTokens())
            {
                if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
                {
                    continue;
                }

                var value = token.ValueText.AsSpan();
                var start = 0;
                while (start < value.Length && char.IsWhiteSpace(value[start]))
                {
                    start++;
                }

                if (start < value.Length)
                {
                    return value[start..].StartsWith(expected, StringComparison.Ordinal);
                }
            }
        }

        return false;
    }

    /// <summary>Finds the first non-whitespace text character of an element and its absolute position.</summary>
    /// <param name="element">The element to scan.</param>
    /// <param name="character">The first non-whitespace character when found.</param>
    /// <param name="position">The absolute source position of that character when found.</param>
    /// <returns><see langword="true"/> when the element has text.</returns>
    public static bool TryGetFirstTextCharacter(XmlElementSyntax element, out char character, out int position)
    {
        character = '\0';
        position = -1;

        foreach (var token in element.DescendantTokens())
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
                    position = token.SpanStart + i;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns the member declaration a documentation node belongs to (hopping out of the structured trivia).</summary>
    /// <param name="nodeInDocumentation">A node inside a documentation comment.</param>
    /// <returns>The documented member declaration, or <see langword="null"/>.</returns>
    public static SyntaxNode? DocumentedMember(SyntaxNode nodeInDocumentation)
        => nodeInDocumentation.FirstAncestorOrSelf<DocumentationCommentTriviaSyntax>()?.ParentTrivia.Token.Parent;

    /// <summary>Returns the local name of an XML element or empty element, or <see langword="null"/> for other nodes.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The element's local name, or <see langword="null"/>.</returns>
    public static string? GetElementName(XmlNodeSyntax node) => node switch
    {
        XmlElementSyntax element => element.StartTag.Name.LocalName.ValueText,
        XmlEmptyElementSyntax element => element.Name.LocalName.ValueText,
        _ => null
    };

    /// <summary>Returns whether a character is terminal sentence punctuation.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for terminal punctuation.</returns>
    private static bool IsTerminalPunctuation(char character)
        => character is '.' or '!' or '?' or ':' or ';';

    /// <summary>Returns whether a character is a closing quote or bracket.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for a closing delimiter.</returns>
    private static bool IsClosingDelimiter(char character)
        => character is '"' or '\'' or ')' or ']' or '”' or '’';

    /// <summary>Returns the last content node of an element that has significant (non-whitespace) text or is an inline element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The last significant content node, or <see langword="null"/>.</returns>
    private static XmlNodeSyntax? LastSignificantContent(XmlElementSyntax element)
    {
        XmlNodeSyntax? lastSignificant = null;
        foreach (var node in element.Content)
        {
            if (node is not XmlTextSyntax text)
            {
                lastSignificant = node;
            }
            else if (ContainsNonWhitespace(text))
            {
                lastSignificant = text;
            }
        }

        return lastSignificant;
    }

    /// <summary>Returns the last two non-whitespace characters (and the last one's position) of a text node.</summary>
    /// <param name="node">The text node.</param>
    /// <param name="last">The last non-whitespace character.</param>
    /// <param name="secondLast">The character before it.</param>
    /// <param name="position">The absolute source position of the last character.</param>
    /// <returns><see langword="true"/> when the node has text.</returns>
    private static bool TryGetTrailingCharacters(XmlNodeSyntax node, out char last, out char secondLast, out int position)
    {
        last = '\0';
        secondLast = '\0';
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
                    secondLast = last;
                    last = text[i];
                    position = token.SpanStart + i;
                }
            }
        }

        return position >= 0;
    }

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

    /// <summary>Records whether the traversal encountered an inheritdoc element.</summary>
    /// <param name="node">The visited XML node.</param>
    /// <param name="found">Whether an inheritdoc element has been found.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> to stop.</returns>
    private static bool VisitInheritDocNode(XmlNodeSyntax node, ref bool found)
    {
        if (GetElementName(node) != "inheritdoc")
        {
            return true;
        }

        found = true;
        return false;
    }
}
