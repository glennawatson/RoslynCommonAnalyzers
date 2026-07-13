// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a line longer than the configured maximum (SST1521), which defaults to 120 characters and is
/// configured with <c>stylesharp.SST1521.max_line_length</c>.
/// </summary>
/// <remarks>
/// <para>
/// A line that cannot be shortened is not reported. The run of non-whitespace characters that crosses the
/// limit is measured: if moving that run onto a line of its own — at the line's own indentation — would
/// still not fit, then no wrap the author can make will help, and the only remaining "fix" is to mangle the
/// content. That exemption is then narrowed to the two places where such a run legitimately occurs: inside a
/// comment (a URL, a licence identifier) and inside a string or character literal (a long path, a query, an
/// encoded blob). A run of code that long is still reported — a hundred-character member chain is unbreakable
/// only in the sense that nobody has broken it yet.
/// </para>
/// <para>
/// The clean path is a subtraction per line. Tabs are counted as one character, because the rule measures the
/// text and not a rendering of it: the width of a tab is an editor setting, and a rule whose verdict changes
/// with the reader's editor is not a rule. The syntax tree is only touched once a line is already over the
/// limit, so a file with no long lines never asks for the root at all.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1521LineTooLongAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.LineTooLong);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports every line over the configured maximum that could actually be wrapped.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var maximum = SizeLimitOptions.ReadMaxLineLength(context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree));
        var text = context.Tree.GetText(context.CancellationToken);
        var lines = text.Lines;
        SyntaxNode? root = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var length = line.End - line.Start;
            if (length <= maximum || IsUnwrappable(context, text, line, maximum, ref root))
            {
                continue;
            }

            var location = Location.Create(context.Tree, TextSpan.FromBounds(line.Start, line.End));
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.LineTooLong, location, length, maximum));
        }
    }

    /// <summary>Returns whether the over-long line carries a run no wrap could rescue.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="line">The over-long line.</param>
    /// <param name="maximum">The configured maximum length.</param>
    /// <param name="root">The lazily fetched syntax root, shared across the file's long lines.</param>
    /// <returns><see langword="true"/> when the line is exempt.</returns>
    private static bool IsUnwrappable(
        SyntaxTreeAnalysisContext context,
        SourceText text,
        TextLine line,
        int maximum,
        ref SyntaxNode? root)
    {
        var limit = line.Start + maximum;
        var runStart = limit;
        while (runStart > line.Start && !char.IsWhiteSpace(text[runStart - 1]))
        {
            runStart--;
        }

        var runEnd = limit;
        while (runEnd < line.End && !char.IsWhiteSpace(text[runEnd]))
        {
            runEnd++;
        }

        // The limit falls on whitespace, so the line already has a break opportunity exactly where it needs one.
        if (runStart == runEnd)
        {
            return false;
        }

        // The run would fit on a line of its own at this indentation, so wrapping is a real option.
        if (CountIndent(text, line) + (runEnd - runStart) <= maximum)
        {
            return false;
        }

        root ??= context.Tree.GetRoot(context.CancellationToken);
        return IsInsideCommentOrLiteral(root, runStart);
    }

    /// <summary>Counts the leading whitespace characters of a line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="line">The line to measure.</param>
    /// <returns>The number of leading whitespace characters.</returns>
    private static int CountIndent(SourceText text, TextLine line)
    {
        var indent = 0;
        while (line.Start + indent < line.End && char.IsWhiteSpace(text[line.Start + indent]))
        {
            indent++;
        }

        return indent;
    }

    /// <summary>Returns whether a position sits inside a comment or inside a string or character literal.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="position">The position of the unbreakable run.</param>
    /// <returns><see langword="true"/> when the content at the position cannot be wrapped by the author.</returns>
    private static bool IsInsideCommentOrLiteral(SyntaxNode root, int position)
    {
        var trivia = root.FindTrivia(position);
        if (IsCommentTrivia(trivia.Kind()))
        {
            return true;
        }

        return IsTextualLiteral(root.FindToken(position).Kind());
    }

    /// <summary>Returns whether a trivia kind is one of the comment forms.</summary>
    /// <param name="kind">The trivia kind.</param>
    /// <returns><see langword="true"/> for a line, block, or documentation comment.</returns>
    private static bool IsCommentTrivia(SyntaxKind kind) => kind is SyntaxKind.SingleLineCommentTrivia
        or SyntaxKind.MultiLineCommentTrivia
        or SyntaxKind.SingleLineDocumentationCommentTrivia
        or SyntaxKind.MultiLineDocumentationCommentTrivia;

    /// <summary>Returns whether a token kind carries author-written text that a wrap would corrupt.</summary>
    /// <param name="kind">The token kind.</param>
    /// <returns><see langword="true"/> for a string, raw string, interpolated-text, or character literal.</returns>
    private static bool IsTextualLiteral(SyntaxKind kind) => kind is SyntaxKind.StringLiteralToken
        or SyntaxKind.CharacterLiteralToken
        or SyntaxKind.InterpolatedStringTextToken
        or SyntaxKind.SingleLineRawStringLiteralToken
        or SyntaxKind.MultiLineRawStringLiteralToken
        or SyntaxKind.Utf8StringLiteralToken
        or SyntaxKind.Utf8SingleLineRawStringLiteralToken
        or SyntaxKind.Utf8MultiLineRawStringLiteralToken;
}
