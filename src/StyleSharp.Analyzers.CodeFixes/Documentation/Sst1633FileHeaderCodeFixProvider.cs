// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Adds the configured file header, replacing an existing (e.g. outdated) header rather than stacking on top of it (SST1633).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1633FileHeaderCodeFixProvider))]
[Shared]
public sealed class Sst1633FileHeaderCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.FileHeader.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            static diagnostic => diagnostic.Properties.TryGetValue(FileHeaderHelper.HeaderProperty, out var header) && !string.IsNullOrEmpty(header) ? "Add file header" : null,
            nameof(Sst1633FileHeaderCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!diagnostic.Properties.TryGetValue(FileHeaderHelper.HeaderProperty, out var header) || string.IsNullOrEmpty(header))
        {
            return;
        }

        changes.Add(BuildChange(text, root, header!));
    }

    /// <summary>Builds the change that replaces any existing header block with the rendered header.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="root">The document's syntax root, or <see langword="null"/> when unavailable.</param>
    /// <param name="header">The rendered header, lines joined by "\n".</param>
    /// <returns>The text change that writes the header at the top of the file.</returns>
    private static TextChange BuildChange(SourceText text, SyntaxNode? root, string header)
    {
        var newLine = DetectNewLine(text);
        var existingEnd = root is null ? 0 : ExistingHeaderEnd(root.GetLeadingTrivia());
        var span = TextSpan.FromBounds(0, existingEnd);
        if (newLine == "\n")
        {
            return new(span, header + newLine);
        }

        var buffer = ArrayPool<char>.Shared.Rent((header.Length + 1) * newLine.Length);
        try
        {
            var length = 0;
            foreach (var character in header)
            {
                if (character == '\n')
                {
                    newLine.CopyTo(0, buffer, length, newLine.Length);
                    length += newLine.Length;
                }
                else
                {
                    buffer[length] = character;
                    length++;
                }
            }

            newLine.CopyTo(0, buffer, length, newLine.Length);
            return new(span, new string(buffer, 0, length + newLine.Length));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Returns the end offset of the existing leading comment header (the run of <c>//</c>/<c>/* */</c>
    /// comments and the single newline that terminates it), or <c>0</c> when the file has no header to
    /// replace. A blank line ends the header, so any comment beyond it is treated as ordinary code and
    /// preserved.
    /// </summary>
    /// <param name="leadingTrivia">The leading trivia of the file's first token.</param>
    /// <returns>The exclusive end offset of the header block, or <c>0</c> when absent.</returns>
    private static int ExistingHeaderEnd(in SyntaxTriviaList leadingTrivia)
    {
        var end = 0;
        var pendingNewLine = false;
        foreach (var trivia in leadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                end = trivia.Span.End;
                pendingNewLine = true;
            }
            else if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (!pendingNewLine)
                {
                    break;
                }

                end = trivia.Span.End;
                pendingNewLine = false;
            }
            else if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                break;
            }
        }

        return end;
    }

    /// <summary>Detects the newline sequence used by the file (defaults to "\n").</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The newline string.</returns>
    private static string DetectNewLine(SourceText text)
    {
        if (text.Lines.Count > 0)
        {
            var first = text.Lines[0];
            var lineBreak = text.ToString(TextSpan.FromBounds(first.End, first.EndIncludingLineBreak));
            if (lineBreak.Length > 0)
            {
                return lineBreak;
            }
        }

        return "\n";
    }
}
