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

    /// <summary>Scans the file for standalone single-line comment blocks and checks their spacing.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        var root = context.Tree.GetRoot(context.CancellationToken);
        var lineCount = text.Lines.Count;

        var line = 0;
        while (line < lineCount)
        {
            if (!TryGetStandaloneComment(root, text, line, out var first))
            {
                line++;
                continue;
            }

            var start = line;
            while (line + 1 < lineCount && TryGetStandaloneComment(root, text, line + 1, out _))
            {
                line++;
            }

            TryGetStandaloneComment(root, text, line, out var last);
            ReportBlock(context, text, start, line, first, last, lineCount);
            line++;
        }
    }

    /// <summary>Reports the preceding- and following-blank-line violations for a comment block.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="start">The first line of the comment block.</param>
    /// <param name="end">The last line of the comment block.</param>
    /// <param name="first">The first comment trivia.</param>
    /// <param name="last">The last comment trivia.</param>
    /// <param name="lineCount">The total line count.</param>
    private static void ReportBlock(SyntaxTreeAnalysisContext context, SourceText text, int start, int end, SyntaxTrivia first, SyntaxTrivia last, int lineCount)
    {
        if (start > 0 && !LayoutHelpers.IsBlankLine(text, start - 1) && !PreviousLineOpensBlock(text, start - 1))
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.SingleLineCommentPrecededByBlankLine, Location.Create(context.Tree, first.Span)));
        }

        if (end + 1 >= lineCount || !LayoutHelpers.IsBlankLine(text, end + 1))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.SingleLineCommentNotFollowedByBlankLine, Location.Create(context.Tree, last.Span)));
    }

    /// <summary>Returns whether the line is a standalone single-line comment (only whitespace before the <c>//</c>).</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="text">The source text.</param>
    /// <param name="lineIndex">The line to inspect.</param>
    /// <param name="comment">The comment trivia, when found.</param>
    /// <returns><see langword="true"/> when the line begins with a single-line comment.</returns>
    private static bool TryGetStandaloneComment(SyntaxNode root, SourceText text, int lineIndex, out SyntaxTrivia comment)
    {
        comment = default;
        var lineSpan = text.Lines[lineIndex];
        for (var position = lineSpan.Start; position < lineSpan.End; position++)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                continue;
            }

            var trivia = root.FindTrivia(position);
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                return false;
            }

            comment = trivia;
            return true;
        }

        return false;
    }

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
