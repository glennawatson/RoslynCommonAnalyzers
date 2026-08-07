// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Collapses a parameter or argument that spans several lines onto one (SST1118). Each wrapped gap inside
/// the item becomes a single space, tightened to nothing where the punctuation reads better closed up.
/// </summary>
/// <remarks>
/// The fix is withheld rather than offered where it would not help: a gap holding a comment cannot be
/// closed without losing the comment, and a collapse that would push the line past the maximum SST1521
/// enforces would only trade one diagnostic for another. Extracting the item into a local is the answer in
/// both cases, and that is a decision for the author.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1118ParameterOnSingleLineCodeFixProvider))]
[Shared]
public sealed class Sst1118ParameterOnSingleLineCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(ReadabilityRules.ParameterMustNotSpanMultipleLines.Id);

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

        var maximum = ReadMaximumLineLength(context.Document, root.SyntaxTree);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (BuildCollapse(text, root, diagnostic, maximum).Count == 0)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Put the parameter on one line",
                    cancellationToken => CollapseAsync(context.Document, diagnostic, cancellationToken),
                    equivalenceKey: nameof(Sst1118ParameterOnSingleLineCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => changes.AddRange(BuildCollapse(text, root, diagnostic, ReadMaximumLineLength(document: null, root.SyntaxTree)));

    /// <summary>Collapses the reported item onto one line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> CollapseAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var changes = BuildCollapse(text, root, diagnostic, ReadMaximumLineLength(document, root.SyntaxTree));
        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Reads the line-length ceiling the collapsed line has to fit inside.</summary>
    /// <param name="document">The document being fixed, when one is available.</param>
    /// <param name="tree">The syntax tree holding the reported item.</param>
    /// <returns>The configured maximum line length.</returns>
    private static int ReadMaximumLineLength(Document? document, SyntaxTree tree)
        => document is null
            ? SizeLimitOptions.DefaultMaxLineLength
            : SizeLimitOptions.ReadMaxLineLength(document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree));

    /// <summary>Builds the changes that close every wrapped gap inside the reported item.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="maximum">The line-length ceiling the result has to respect.</param>
    /// <returns>The changes, or an empty list when the item cannot be collapsed.</returns>
    private static List<TextChange> BuildCollapse(SourceText text, SyntaxNode root, Diagnostic diagnostic, int maximum)
    {
        var changes = new List<TextChange>();
        var span = diagnostic.Location.SourceSpan;
        var item = root.FindNode(span);
        if (item.Span != span)
        {
            return changes;
        }

        var collapsed = 0;
        var token = item.GetFirstToken();
        var last = item.GetLastToken();
        while (!token.Equals(last))
        {
            var next = token.GetNextToken();
            LayoutHelpers.ClassifyGap(text, token.Span.End, next.SpanStart, out var hasLineBreak, out var isClean);
            if (hasLineBreak && !isClean)
            {
                // A comment lives in the gap; closing it up would lose the comment.
                changes.Clear();
                return changes;
            }

            if (hasLineBreak)
            {
                var replacement = Tighten(token, next) ? string.Empty : " ";
                collapsed += next.SpanStart - token.Span.End - replacement.Length;
                changes.Add(new TextChange(TextSpan.FromBounds(token.Span.End, next.SpanStart), replacement));
            }

            token = next;
        }

        if (changes.Count > 0 && CollapsedLineLength(text, item, collapsed) > maximum)
        {
            changes.Clear();
        }

        return changes;
    }

    /// <summary>Returns the length the item's line would have once every wrapped gap is closed.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="item">The reported parameter or argument.</param>
    /// <param name="collapsed">The number of characters the collapse removes.</param>
    /// <returns>The resulting line length.</returns>
    private static int CollapsedLineLength(SourceText text, SyntaxNode item, int collapsed)
    {
        var firstLine = text.Lines.GetLineFromPosition(item.SpanStart);
        var lastLine = text.Lines.GetLineFromPosition(item.Span.End);
        return lastLine.End - firstLine.Start - collapsed;
    }

    /// <summary>Returns whether two tokens read better with no space between them.</summary>
    /// <param name="token">The earlier token.</param>
    /// <param name="next">The later token.</param>
    /// <returns><see langword="true"/> when the gap should close to nothing.</returns>
    private static bool Tighten(SyntaxToken token, SyntaxToken next)
        => token.IsKind(SyntaxKind.OpenParenToken)
            || token.IsKind(SyntaxKind.OpenBracketToken)
            || next.IsKind(SyntaxKind.CloseParenToken)
            || next.IsKind(SyntaxKind.CloseBracketToken)
            || next.IsKind(SyntaxKind.CommaToken);
}
