// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an SST2243 verbatim string literal as a raw string literal. The delimiter grows one
/// quote past the longest quote run in the value (never below three). Single-line literals stay on
/// one line with the doubled-quote escapes unescaped; multi-line literals hang each value line
/// between the delimiters, prefixed with the indentation of the literal's start line, so the value
/// stays character-for-character identical (whitespace-only value lines become empty lines).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2243UseRawStringLiteralCodeFixProvider))]
[Shared]
public sealed class Sst2243UseRawStringLiteralCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The smallest raw string delimiter the language allows.</summary>
    private const int MinimumDelimiterLength = 3;

    /// <summary>The character count of a CRLF line-break pair.</summary>
    private const int CrlfLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseRawStringLiteral.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetLiteral(root, diagnostic, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use a raw string literal",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(Sst2243UseRawStringLiteralCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetLiteral(editor.OriginalRoot, diagnostic, out var literal))
        {
            return;
        }

        editor.ReplaceNode(literal!, BuildReplacement(literal!));
    }

    /// <summary>Applies the raw-string rewrite for one diagnostic.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original document when the diagnostic no longer resolves.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
        => TryGetLiteral(root, diagnostic, out var literal)
            ? document.WithSyntaxRoot(root.ReplaceNode(literal!, BuildReplacement(literal!)))
            : document;

    /// <summary>Resolves the diagnostic to the reported verbatim string literal.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="literal">The verbatim string literal expression.</param>
    /// <returns><see langword="true"/> when the literal was found and is still verbatim.</returns>
    private static bool TryGetLiteral(SyntaxNode root, Diagnostic diagnostic, out LiteralExpressionSyntax? literal)
    {
        literal = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as LiteralExpressionSyntax;
        if (literal?.IsKind(SyntaxKind.StringLiteralExpression) != true)
        {
            literal = null;
            return false;
        }

        var text = literal.Token.Text;
        if (text.Length > 0 && text[0] == '@')
        {
            return true;
        }

        literal = null;
        return false;
    }

    /// <summary>Builds the raw string literal expression replacing the verbatim literal.</summary>
    /// <param name="literal">The verbatim string literal to rewrite.</param>
    /// <returns>The raw string literal expression carrying the original trivia.</returns>
    private static ExpressionSyntax BuildReplacement(LiteralExpressionSyntax literal)
    {
        var value = literal.Token.ValueText;
        var delimiter = new string('"', DelimiterLength(value));
        var rawText = HasNewline(value)
            ? BuildMultiLineText(literal, value, delimiter)
            : delimiter + value + delimiter;
        return SyntaxFactory.ParseExpression(rawText).WithTriviaFrom(literal);
    }

    /// <summary>Builds the multi-line raw string token text for a value containing line breaks.</summary>
    /// <param name="literal">The literal being rewritten, used to detect the newline and indentation.</param>
    /// <param name="value">The literal's value.</param>
    /// <param name="delimiter">The raw string delimiter.</param>
    /// <returns>The raw string token text with the delimiters on their own lines.</returns>
    private static string BuildMultiLineText(LiteralExpressionSyntax literal, string value, string delimiter)
    {
        var text = literal.SyntaxTree.GetText();
        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var indent = LayoutFixHelpers.IndentOfLine(text, literal.SpanStart);
        var lineCount = CountLines(value);
        var capacity = value.Length + (2 * delimiter.Length) + ((lineCount + 1) * (indent.Length + newLine.Length));
        var builder = new StringBuilder(capacity);
        builder.Append(delimiter).Append(newLine);

        var start = 0;
        var index = 0;
        while (index < value.Length)
        {
            var c = value[index];
            if (c is not ('\n' or '\r'))
            {
                index++;
                continue;
            }

            AppendContentLine(builder, value, start, index, indent, newLine);
            index = c == '\r' && index + 1 < value.Length && value[index + 1] == '\n' ? index + CrlfLength : index + 1;
            start = index;
        }

        AppendContentLine(builder, value, start, value.Length, indent, newLine);
        return builder.Append(indent).Append(delimiter).ToString();
    }

    /// <summary>Appends one value line, prefixed with the indent, or an empty line when the value line is whitespace-only.</summary>
    /// <param name="builder">The token text builder.</param>
    /// <param name="value">The literal's value.</param>
    /// <param name="start">The inclusive start of the value line.</param>
    /// <param name="end">The exclusive end of the value line.</param>
    /// <param name="indent">The indentation prefix for content lines.</param>
    /// <param name="newLine">The newline sequence.</param>
    private static void AppendContentLine(StringBuilder builder, string value, int start, int end, string indent, string newLine)
    {
        var whitespaceOnly = true;
        for (var i = start; i < end; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                whitespaceOnly = false;
                break;
            }
        }

        if (!whitespaceOnly)
        {
            builder.Append(indent).Append(value, start, end - start);
        }

        builder.Append(newLine);
    }

    /// <summary>Returns the raw string delimiter length: one past the longest quote run in the value, at least three.</summary>
    /// <param name="value">The literal's value.</param>
    /// <returns>The number of quote characters in each delimiter.</returns>
    private static int DelimiterLength(string value)
    {
        var longestRun = 0;
        var run = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '"')
            {
                run = 0;
                continue;
            }

            run++;
            if (run > longestRun)
            {
                longestRun = run;
            }
        }

        var length = longestRun + 1;
        return length < MinimumDelimiterLength ? MinimumDelimiterLength : length;
    }

    /// <summary>Returns whether the value contains a line break.</summary>
    /// <param name="value">The literal's value.</param>
    /// <returns><see langword="true"/> when the value spans multiple lines.</returns>
    private static bool HasNewline(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is '\n' or '\r')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts the value lines, treating CRLF as one line break.</summary>
    /// <param name="value">The literal's value.</param>
    /// <returns>The number of lines the value spans.</returns>
    private static int CountLines(string value)
    {
        var lines = 1;
        var index = 0;
        while (index < value.Length)
        {
            var c = value[index];
            if (c is not ('\n' or '\r'))
            {
                index++;
                continue;
            }

            lines++;
            index = c == '\r' && index + 1 < value.Length && value[index + 1] == '\n' ? index + CrlfLength : index + 1;
        }

        return lines;
    }
}
