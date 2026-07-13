// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Counts the lines of a subtree that actually carry code, for the size-metric rules
/// (SST1522–SST1524). A line counts when at least one token sits on it, which makes blank lines,
/// comment-only lines, documentation headers and directive-only lines free — they are trivia, and
/// trivia is never visited. A multi-line string literal is one token spanning several lines, and
/// every one of those lines counts, because they are all lines a reader has to scroll past.
/// </summary>
/// <remarks>
/// The walk is an allocation-free indexed traversal, not an iterator, and it is never entered on the
/// clean path: the raw line span of a node is an upper bound on its code lines, so a member already
/// inside its limit is rejected on two line lookups before any token is touched. Tokens arrive in
/// document order, so the line table is read through a single monotonic cursor rather than a binary
/// search per token.
/// </remarks>
internal static class CodeLineCounter
{
    /// <summary>Counts the lines beneath a node that carry at least one token.</summary>
    /// <param name="text">The source text that owns the node.</param>
    /// <param name="node">The node to measure.</param>
    /// <returns>The number of distinct lines that carry code.</returns>
    public static int Count(SourceText text, SyntaxNode node)
    {
        var state = new LineTally(text);
        DescendantTraversalHelper.VisitDescendantTokens(node, ref state, AddToken);
        return state.CountedLines;
    }

    /// <summary>Returns the number of lines a span covers, which bounds the code lines it can hold.</summary>
    /// <param name="text">The source text that owns the span.</param>
    /// <param name="span">The span to measure.</param>
    /// <returns>The inclusive line count of the span.</returns>
    public static int SpannedLines(SourceText text, TextSpan span)
    {
        var startLine = LayoutHelpers.LineOf(text, span.Start);
        var endLine = span.End > span.Start ? LayoutHelpers.LineOf(text, span.End - 1) : startLine;
        return endLine - startLine + 1;
    }

    /// <summary>Adds one token's lines to the running tally.</summary>
    /// <param name="token">The token being visited.</param>
    /// <param name="state">The running tally.</param>
    /// <returns>Always <see langword="true"/>; every token is visited.</returns>
    private static bool AddToken(in SyntaxToken token, ref LineTally state)
    {
        state.Add(token.Span);
        return true;
    }

    /// <summary>Tallies the distinct lines covered by a document-ordered sequence of spans.</summary>
    private record struct LineTally
    {
        /// <summary>The source text being measured.</summary>
        private readonly SourceText _text;

        /// <summary>The current line-table cursor.</summary>
        private int _lineNumber;

        /// <summary>The line at the current cursor.</summary>
        private TextLine _line;

        /// <summary>The highest line already counted, or -1 before the first span.</summary>
        private int _highestCounted;

        /// <summary>The number of distinct lines counted so far.</summary>
        private int _count;

        /// <summary>Initializes a new instance of the <see cref="LineTally"/> struct.</summary>
        /// <param name="text">The source text being measured.</param>
        public LineTally(SourceText text)
        {
            _text = text;
            _lineNumber = 0;
            _line = text.Lines.Count > 0 ? text.Lines[0] : default;
            _highestCounted = -1;
            _count = 0;
        }

        /// <summary>Gets the number of distinct lines counted so far.</summary>
        public readonly int CountedLines => _count;

        /// <summary>Counts the lines a span covers that have not been counted already.</summary>
        /// <param name="span">The span to add; spans arrive in document order.</param>
        public void Add(TextSpan span)
        {
            if (span.IsEmpty)
            {
                return;
            }

            LayoutHelpers.GetLineSpanOfOrLater(_text, span.Start, span.End, ref _lineNumber, ref _line, out var startLine, out var endLine);
            var from = startLine > _highestCounted ? startLine : _highestCounted + 1;
            if (endLine < from)
            {
                return;
            }

            _count += endLine - from + 1;
            _highestCounted = endLine;
        }
    }
}
