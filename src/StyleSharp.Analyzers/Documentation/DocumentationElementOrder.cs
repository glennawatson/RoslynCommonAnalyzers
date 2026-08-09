// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Holds the conventional order of the documentation elements and finds the first pair a comment writes out
/// of sequence. Shared by the analyzer and its code fix so the two agree on what the order is.
/// </summary>
internal static class DocumentationElementOrder
{
    /// <summary>The element names in the order they are conventionally written.</summary>
    /// <remarks>
    /// Small and searched linearly on purpose: a documentation comment holds a handful of elements, so a scan
    /// of nine strings beats hashing each name. Anything not listed here has no rank and is never moved.
    /// </remarks>
    private static readonly string[] RankedNames =
    [
        "summary",
        "typeparam",
        "param",
        "returns",
        "value",
        "exception",
        "remarks",
        "example",
        "seealso",
    ];

    /// <summary>Gets the conventional rank of a documentation element name.</summary>
    /// <param name="name">The element's local name.</param>
    /// <returns>The rank, or -1 when the element has no place in the conventional order.</returns>
    public static int RankOf(string name)
    {
        for (var i = 0; i < RankedNames.Length; i++)
        {
            if (string.Equals(RankedNames[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Gets the local name of a documentation element node.</summary>
    /// <param name="node">The content node.</param>
    /// <returns>The element's local name, or <see langword="null"/> when the node is not an element.</returns>
    public static string? NameOf(XmlNodeSyntax node) => node switch
    {
        XmlElementSyntax element => element.StartTag.Name.LocalName.ValueText,
        XmlEmptyElementSyntax empty => empty.Name.LocalName.ValueText,
        _ => null,
    };

    /// <summary>Finds the first element that is written after one it should come before.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="outOfOrder">The element written too early.</param>
    /// <param name="shouldPrecede">The name of the element it should have come before.</param>
    /// <returns><see langword="true"/> when the comment writes two ranked elements out of sequence.</returns>
    public static bool TryFindFirstOutOfOrder(
        DocumentationCommentTriviaSyntax documentation,
        out XmlNodeSyntax outOfOrder,
        out string shouldPrecede)
    {
        outOfOrder = null!;
        shouldPrecede = string.Empty;

        var highestRank = -1;
        var highestName = string.Empty;
        var content = documentation.Content;
        for (var i = 0; i < content.Count; i++)
        {
            var node = content[i];
            if (NameOf(node) is not { } name)
            {
                continue;
            }

            var rank = RankOf(name);
            if (rank < 0)
            {
                continue;
            }

            if (rank < highestRank)
            {
                outOfOrder = node;
                shouldPrecede = highestName;
                return true;
            }

            highestRank = rank;
            highestName = name;
        }

        return false;
    }
}
