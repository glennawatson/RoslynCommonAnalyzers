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
/// a block (directly after an opening brace) or begins the file is exempt from SST1515.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleLineCommentSpacingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        LayoutRules.SingleLineCommentPrecededByBlankLine,
        LayoutRules.SingleLineCommentNotFollowedByBlankLine);

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
    internal static bool IsStandaloneComment(SourceText text, SyntaxTrivia comment)
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
        var firstTokenStart = root.GetFirstToken().SpanStart;

        var start = -1;
        var end = -1;
        var first = default(SyntaxTrivia);
        var last = default(SyntaxTrivia);

        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || !IsStandaloneComment(text, trivia))
            {
                continue;
            }

            var line = text.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
            if (start < 0)
            {
                start = line;
                end = line;
                first = trivia;
                last = trivia;
                continue;
            }

            if (line == end + 1)
            {
                end = line;
                last = trivia;
                continue;
            }

            ReportBlock(context, text, start, end, first, last, firstTokenStart);
            start = line;
            end = line;
            first = trivia;
            last = trivia;
        }

        if (start < 0)
        {
            return;
        }

        ReportBlock(context, text, start, end, first, last, firstTokenStart);
    }

    /// <summary>Reports the preceding- and following-blank-line violations for a comment block.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="start">The first line of the comment block.</param>
    /// <param name="end">The last line of the comment block.</param>
    /// <param name="first">The first comment trivia.</param>
    /// <param name="last">The last comment trivia.</param>
    /// <param name="firstTokenStart">The start position of the first token in the file.</param>
    private static void ReportBlock(SyntaxTreeAnalysisContext context, SourceText text, int start, int end, SyntaxTrivia first, SyntaxTrivia last, int firstTokenStart)
    {
        if (start > 0 && !LayoutHelpers.IsBlankLine(text, start - 1) && !PreviousLineOpensBlock(text, start - 1))
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
    private static bool IsFileHeaderBlock(SyntaxTrivia last, int firstTokenStart)
        => last.SpanStart < firstTokenStart;

    /// <summary>Returns whether the last non-whitespace character of the line is an opening brace.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="lineIndex">The line to inspect.</param>
    /// <returns><see langword="true"/> when the line ends with an opening brace.</returns>
    private static bool PreviousLineOpensBlock(SourceText text, int lineIndex)
    {
        var lineSpan = text.Lines[lineIndex];
        for (var position = lineSpan.End - 1; position >= lineSpan.Start; position--)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                continue;
            }

            return text[position] == '{';
        }

        return false;
    }
}
