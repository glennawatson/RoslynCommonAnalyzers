// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
public sealed class Sst1659EmptyCommentCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.EmptyComment.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Remove the empty comment",
            nameof(Sst1659EmptyCommentCodeFixProvider),
            static (document, diagnostic, cancellationToken) => CommentRemovalHelper.RemoveAsync(document, diagnostic.Location.SourceSpan, cancellationToken));

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes) =>
        changes.Add(CommentRemovalHelper.RemovalChange(text, diagnostic.Location.SourceSpan));
}
