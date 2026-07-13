// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an empty comment (SST1120). When the comment is the only content on its line the
/// whole line is removed; when it trails code only the comment and the whitespace before it
/// are removed.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1120CommentContentCodeFixProvider))]
[Shared]
public sealed class Sst1120CommentContentCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.CommentMustContainText.Id);

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
                    "Remove empty comment",
                    cancellationToken => RemoveAsync(context.Document, span, cancellationToken),
                    equivalenceKey: nameof(Sst1120CommentContentCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => changes.Add(new TextChange(CommentRemovalHelper.ComputeRemoval(text, diagnostic.Location.SourceSpan), string.Empty));

    /// <summary>Computes and applies the removal of the empty comment.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="span">The comment span.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> RemoveAsync(Document document, TextSpan span, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var removal = CommentRemovalHelper.ComputeRemoval(text, span);
        return document.WithText(text.WithChanges(new TextChange(removal, string.Empty)));
    }
}
