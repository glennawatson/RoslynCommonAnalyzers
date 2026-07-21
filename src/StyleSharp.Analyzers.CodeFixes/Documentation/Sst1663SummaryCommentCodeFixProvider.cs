// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Converts a summary-like <c>//</c> comment into a <c>/// &lt;summary&gt;</c> documentation comment (SST1663),
/// XML-escaping the text so the result is a well-formed documentation comment.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1663SummaryCommentCodeFixProvider))]
[Shared]
public sealed class Sst1663SummaryCommentCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DocumentationRules.SummaryComment.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

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
            if (!TryBuildChange(root, diagnostic, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Convert to a '/// <summary>' documentation comment",
                    cancellationToken => ConvertAsync(context.Document, diagnostic, cancellationToken),
                    equivalenceKey: nameof(Sst1663SummaryCommentCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryBuildChange(root, diagnostic, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Applies the comment conversion to the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ConvertAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || !TryBuildChange(root, diagnostic, out var change))
        {
            return document;
        }

        return document.WithText(text.WithChanges(change));
    }

    /// <summary>Builds the change that rewrites the <c>//</c> comment as a <c>/// &lt;summary&gt;</c> line.</summary>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="change">The rewrite change when one is produced.</param>
    /// <returns><see langword="true"/> when a change was built.</returns>
    private static bool TryBuildChange(SyntaxNode root, Diagnostic diagnostic, out TextChange change)
    {
        change = default;

        var trivia = root.FindTrivia(diagnostic.Location.SourceSpan.Start);
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
        {
            return false;
        }

        var raw = trivia.ToString();
        var content = Escape(raw.Substring(2).Trim());
        change = new TextChange(trivia.Span, "/// <summary>" + content + "</summary>");
        return true;
    }

    /// <summary>Escapes the XML-significant characters in a run of comment text.</summary>
    /// <param name="value">The comment text.</param>
    /// <returns>The XML-escaped text.</returns>
    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
