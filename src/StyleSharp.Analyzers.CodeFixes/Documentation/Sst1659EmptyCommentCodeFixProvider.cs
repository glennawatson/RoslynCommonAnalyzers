// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a comment that has no text (SST1659). A comment that owns its line takes the line with it, so no
/// blank line is left behind; a comment that trails code is removed together with the whitespace that
/// separated it from that code. An empty documentation comment spanning several <c>///</c> lines is removed
/// as a whole.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1659EmptyCommentCodeFixProvider))]
[Shared]
public sealed class Sst1659EmptyCommentCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.EmptyComment.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var span = diagnostic.Location.SourceSpan;
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the empty comment",
                    cancellationToken => RemoveAsync(context.Document, span, cancellationToken),
                    equivalenceKey: nameof(Sst1659EmptyCommentCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => changes.Add(new TextChange(CommentRemovalHelper.ComputeRemoval(text, diagnostic.Location.SourceSpan), string.Empty));

    /// <summary>Removes the empty comment from the document.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="span">The comment span.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> RemoveAsync(Document document, TextSpan span, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var removal = CommentRemovalHelper.ComputeRemoval(text, span);
        return document.WithText(text.WithChanges(new TextChange(removal, string.Empty)));
    }
}
