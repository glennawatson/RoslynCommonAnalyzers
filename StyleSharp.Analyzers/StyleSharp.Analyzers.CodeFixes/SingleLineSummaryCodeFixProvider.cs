// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Collapses a short multi-line <c>&lt;summary&gt;</c> onto a single line (SST1653).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SingleLineSummaryCodeFixProvider))]
[Shared]
public sealed class SingleLineSummaryCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.SingleLineSummary.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
            var node = root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
            if (node.FirstAncestorOrSelf<XmlElementSyntax>() is not { } summary)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Put summary on a single line",
                    cancellationToken => CollapseAsync(context.Document, summary, cancellationToken),
                    equivalenceKey: nameof(SingleLineSummaryCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Rewrites the summary element's text so the tags and content sit on one line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="summary">The summary element to collapse.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> CollapseAsync(Document document, XmlElementSyntax summary, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

        var innerSpan = TextSpan.FromBounds(summary.StartTag.Span.End, summary.EndTag.Span.Start);
        var collapsed = Collapse(text.ToString(innerSpan));

        return document.WithText(text.Replace(summary.Span, "<summary>" + collapsed + "</summary>"));
    }

    /// <summary>Strips <c>///</c> exteriors and collapses whitespace runs to single spaces, trimming the ends.</summary>
    /// <param name="inner">The raw text between the summary tags.</param>
    /// <returns>The single-line inner text.</returns>
    private static string Collapse(string inner)
    {
        const string exterior = "///";

        var builder = new StringBuilder(inner.Length);
        var started = false;
        var pendingSpace = false;
        var i = 0;

        while (i < inner.Length)
        {
            // Skip the '///' documentation exterior that prefixes each line.
            if (StartsWith(inner, i, exterior))
            {
                i += exterior.Length;
                continue;
            }

            var character = inner[i];
            i++;

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = started;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
            started = true;
        }

        return builder.ToString();
    }

    /// <summary>Returns whether <paramref name="text"/> contains <paramref name="value"/> starting at <paramref name="index"/>.</summary>
    /// <param name="text">The text to test.</param>
    /// <param name="index">The position to test at.</param>
    /// <param name="value">The substring to look for.</param>
    /// <returns><see langword="true"/> on a match.</returns>
    private static bool StartsWith(string text, int index, string value)
        => index + value.Length <= text.Length
            && string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
}
