// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Wraps each blank-line-separated paragraph of a pure-prose <c>&lt;summary&gt;</c> in a <c>&lt;para&gt;</c>
/// element (SST1664), dropping the now-redundant blank documentation lines.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1664SummaryParagraphCodeFixProvider))]
[Shared]
public sealed class Sst1664SummaryParagraphCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DocumentationRules.SummaryParagraph.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryBuildChange(text, root, diagnostic, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Wrap the paragraphs in <para> elements",
                    cancellationToken => WrapAsync(context.Document, diagnostic, cancellationToken),
                    equivalenceKey: nameof(Sst1664SummaryParagraphCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryBuildChange(text, root, diagnostic, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Applies the paragraph wrapping to the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> WrapAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || !TryBuildChange(text, root, diagnostic, out var change))
        {
            return document;
        }

        return document.WithText(text.WithChanges(change));
    }

    /// <summary>Builds the change that rewrites the summary's inner lines with <c>&lt;para&gt;</c> wrappers.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="change">The rewrite change when one is produced.</param>
    /// <returns><see langword="true"/> when a change was built.</returns>
    private static bool TryBuildChange(SourceText text, SyntaxNode root, Diagnostic diagnostic, out TextChange change)
    {
        change = default;

        var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
        if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { StartTag.Name.LocalName.ValueText: "summary" } summary
            || !SummaryParagraphLayout.TryGetInnerLineRange(text, summary, out var firstInnerLine, out var lastInnerLine))
        {
            return false;
        }

        var startLine = text.Lines[firstInnerLine];
        var indent = text.ToString(TextSpan.FromBounds(text.Lines[firstInnerLine - 1].Start, IndentEnd(text, firstInnerLine - 1)));
        var newLine = NewLine(text, startLine.End, startLine.EndIncludingLineBreak);

        var builder = new StringBuilder();
        var inParagraph = false;
        for (var lineNumber = firstInnerLine; lineNumber <= lastInnerLine; lineNumber++)
        {
            var line = text.Lines[lineNumber];
            if (SummaryParagraphLayout.LineHasProse(text, line.Span))
            {
                if (!inParagraph)
                {
                    builder.Append(indent).Append("/// <para>").Append(newLine);
                    inParagraph = true;
                }

                builder.Append(text.ToString(TextSpan.FromBounds(line.Start, line.EndIncludingLineBreak)));
            }
            else if (inParagraph)
            {
                builder.Append(indent).Append("/// </para>").Append(newLine);
                inParagraph = false;
            }
        }

        if (inParagraph)
        {
            builder.Append(indent).Append("/// </para>").Append(newLine);
        }

        var replaceStart = text.Lines[firstInnerLine].Start;
        var replaceEnd = text.Lines[lastInnerLine + 1].Start;
        change = new TextChange(TextSpan.FromBounds(replaceStart, replaceEnd), builder.ToString());
        return true;
    }

    /// <summary>Returns the position of the first non-whitespace character on a line (the end of its indentation).</summary>
    /// <param name="text">The source text.</param>
    /// <param name="lineNumber">The line number.</param>
    /// <returns>The position where the line's indentation ends.</returns>
    private static int IndentEnd(SourceText text, int lineNumber)
    {
        var line = text.Lines[lineNumber];
        var i = line.Start;
        while (i < line.End && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>Returns the line-break text between a line's end and its break-inclusive end, defaulting to a line feed.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="end">The line end position.</param>
    /// <param name="endIncludingBreak">The line end position including the break.</param>
    /// <returns>The line-break text.</returns>
    private static string NewLine(SourceText text, int end, int endIncludingBreak)
    {
        var newLine = text.ToString(TextSpan.FromBounds(end, endIncludingBreak));
        return newLine.Length == 0 ? "\n" : newLine;
    }
}
