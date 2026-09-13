// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the blank-line spacing around a standalone single-line comment block: a block
/// that is not preceded by a blank line (SST1515) and a block that is followed by a blank
/// line (SST1512). Consecutive comment lines form one block; the first line carries the
/// preceding-blank check and the last line the following-blank check. A comment that opens
/// a body (directly after an opening brace or an expression-body <c>=&gt;</c>), follows a
/// preprocessor directive (e.g. the first line inside an <c>#if</c>/<c>#else</c> branch), or
/// begins the file is exempt from SST1515.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleLineCommentSpacingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
        LayoutRules.SingleLineCommentPrecededByBlankLine,
        LayoutRules.SingleLineCommentNotFollowedByBlankLine);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Returns whether the trivia is a standalone single-line comment (only whitespace before the <c>//</c>).</summary>
    /// <param name="text">The source text.</param>
    /// <param name="comment">The comment trivia to inspect.</param>
    /// <returns><see langword="true"/> when the line begins with a single-line comment.</returns>
    internal static bool IsStandaloneComment(SourceText text, in SyntaxTrivia comment)
    {
        var lineSpan = text.Lines.GetLineFromPosition(comment.SpanStart).Span;
        for (var position = lineSpan.Start; position < comment.SpanStart; position++)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>Scans the file for standalone single-line comment blocks and checks their spacing.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        var root = context.Tree.GetRoot(context.CancellationToken);

        // Zero-width tokens count, because the end-of-file token may be the only one there is. A file whose
        // whole body sits inside an inactive '#if' has no other, and the default GetFirstToken() skips it and
        // hands back a default token starting at 0 — which puts the file header *after* the first token and
        // collapses the header exemption, reporting the copyright banner of every file compiled out.
        var firstTokenStart = root.GetFirstToken(includeZeroWidth: true).SpanStart;

        var state = new CommentBlockState(context, text, firstTokenStart);
        _ = DescendantTraversalHelper.VisitDescendantTokens(
            root,
            ref state,
            static (in SyntaxToken token, ref CommentBlockState scan) =>
            {
                scan.Observe(token.LeadingTrivia);
                scan.Observe(token.TrailingTrivia);
                return true;
            });

        if (state.Start < 0)
        {
            return;
        }

        ReportBlock(context, text, state.Start, state.End, state.First, state.Last, firstTokenStart);
    }

    /// <summary>Reports the preceding- and following-blank-line violations for a comment block.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="start">The first line of the comment block.</param>
    /// <param name="end">The last line of the comment block.</param>
    /// <param name="first">The first comment trivia.</param>
    /// <param name="last">The last comment trivia.</param>
    /// <param name="firstTokenStart">The start position of the first token in the file.</param>
    private static void ReportBlock(in SyntaxTreeAnalysisContext context, SourceText text, int start, int end, in SyntaxTrivia first, in SyntaxTrivia last, int firstTokenStart)
    {
        if (start > 0 && !LayoutHelpers.IsBlankLine(text, start - 1) && !PreviousLineOpensBody(text, start - 1) && !PreviousLineIsDirective(text, start - 1))
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.SingleLineCommentPrecededByBlankLine, Location.Create(context.Tree, first.Span)));
        }

        if (end + 1 >= text.Lines.Count || !LayoutHelpers.IsBlankLine(text, end + 1) || IsFileHeaderBlock(last, firstTokenStart))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.SingleLineCommentNotFollowedByBlankLine, Location.Create(context.Tree, last.Span)));
    }

    /// <summary>Returns whether the comment block belongs to the leading file header rather than an interior comment block.</summary>
    /// <param name="last">The last comment trivia in the block.</param>
    /// <param name="firstTokenStart">The start position of the first token in the file.</param>
    /// <returns><see langword="true"/> when the block sits before the file's first token.</returns>
    private static bool IsFileHeaderBlock(in SyntaxTrivia last, int firstTokenStart) =>
        last.SpanStart < firstTokenStart;

    /// <summary>Returns whether the line is a preprocessor directive (its first non-whitespace character is <c>#</c>).</summary>
    /// <param name="text">The source text.</param>
    /// <param name="lineIndex">The line to inspect.</param>
    /// <returns><see langword="true"/> when the line begins (after indentation) with a directive.</returns>
    private static bool PreviousLineIsDirective(SourceText text, int lineIndex)
    {
        var lineSpan = text.Lines[lineIndex];
        for (var position = lineSpan.Start; position < lineSpan.End; position++)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                continue;
            }

            return text[position] == '#';
        }

        return false;
    }

    /// <summary>Returns whether the line ends by opening a body the comment introduces.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="lineIndex">The line to inspect.</param>
    /// <returns><see langword="true"/> when the line ends with an opening brace or an expression-body arrow.</returns>
    /// <remarks>
    /// An arrow counts for the same reason a brace does: the comment below it heads the body rather than
    /// trailing the code above, and there is nowhere to put a blank line — one after the arrow is itself
    /// reported by SST1537.
    /// </remarks>
    private static bool PreviousLineOpensBody(SourceText text, int lineIndex)
    {
        var lineSpan = text.Lines[lineIndex];
        for (var position = lineSpan.End - 1; position >= lineSpan.Start; position--)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                continue;
            }

            return text[position] == '{'
                || (text[position] == '>' && position > lineSpan.Start && text[position - 1] == '=');
        }

        return false;
    }

    /// <summary>Groups standalone comments across token boundaries in source order.</summary>
    private record struct CommentBlockState
    {
        /// <summary>The context receiving spacing diagnostics.</summary>
        private readonly SyntaxTreeAnalysisContext _context;

        /// <summary>The source text used to locate comment lines.</summary>
        private readonly SourceText _text;

        /// <summary>The position separating the file header from interior comments.</summary>
        private readonly int _firstTokenStart;

        /// <summary>The first comment in the current block.</summary>
        private SyntaxTrivia _first;

        /// <summary>The last comment in the current block.</summary>
        private SyntaxTrivia _last;

        /// <summary>The first line in the current block, or -1 before a comment is found.</summary>
        private int _start;

        /// <summary>The last line in the current block.</summary>
        private int _end;

        /// <summary>Initializes a new instance of the <see cref="CommentBlockState"/> struct.</summary>
        /// <param name="context">The context receiving spacing diagnostics.</param>
        /// <param name="text">The source text.</param>
        /// <param name="firstTokenStart">The first token's position, including zero-width tokens.</param>
        public CommentBlockState(in SyntaxTreeAnalysisContext context, SourceText text, int firstTokenStart)
        {
            _context = context;
            _text = text;
            _first = default;
            _last = default;
            _firstTokenStart = firstTokenStart;
            _start = -1;
            _end = -1;
        }

        /// <summary>Gets the first line in the current block, or -1 when no comment was found.</summary>
        public readonly int Start => _start;

        /// <summary>Gets the last line in the current block.</summary>
        public readonly int End => _end;

        /// <summary>Gets the first comment in the current block.</summary>
        public readonly SyntaxTrivia First => _first;

        /// <summary>Gets the last comment in the current block.</summary>
        public readonly SyntaxTrivia Last => _last;

        /// <summary>Adds standalone comments and reports a block when a later block begins.</summary>
        /// <param name="triviaList">One token's leading or trailing trivia in source order.</param>
        public void Observe(in SyntaxTriviaList triviaList)
        {
            foreach (var trivia in triviaList)
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || !IsStandaloneComment(_text, trivia))
                {
                    continue;
                }

                var line = _text.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
                if (_start < 0)
                {
                    _start = line;
                    _end = line;
                    _first = trivia;
                    _last = trivia;
                    continue;
                }

                if (line == _end + 1)
                {
                    _end = line;
                    _last = trivia;
                    continue;
                }

                ReportBlock(_context, _text, _start, _end, _first, _last, _firstTokenStart);
                _start = line;
                _end = line;
                _first = trivia;
                _last = trivia;
            }
        }
    }
}
