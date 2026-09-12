// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Deletes a run of blank lines that has no place in the file: consecutive blank lines within it
/// (SST1507), or blank lines ahead of its first line of code (SST1517). Both rules report the whole run
/// they object to, so the repair is to delete the reported span; only the wording of the offered action
/// differs. Blank lines beside a brace are delimited by the brace rather than by the report, and are
/// fixed by <see cref="BlankLineRemovalCodeFixProvider"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExcessBlankLineCodeFixProvider))]
[Shared]
public sealed class ExcessBlankLineCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <summary>The action offered for a run of consecutive blank lines.</summary>
    private const string RemoveExtraBlankLinesTitle = "Remove extra blank lines";

    /// <summary>The action offered for blank lines ahead of the first line of code.</summary>
    private const string RemoveLeadingBlankLinesTitle = "Remove blank lines at start of file";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.MultipleBlankLines.Id,
        LayoutRules.NoBlankLinesAtStartOfFile.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    GetTitle(diagnostic),
                    cancellationToken => RemoveAsync(context.Document, diagnostic.Location.SourceSpan, cancellationToken),
                    equivalenceKey: nameof(ExcessBlankLineCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes) =>
        changes.Add(new(diagnostic.Location.SourceSpan, string.Empty));

    /// <summary>Deletes the reported span.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="span">The span covering the blank lines.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> RemoveAsync(Document document, TextSpan span, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(new TextChange(span, string.Empty)));
    }

    /// <summary>Returns the action wording matching the rule that reported the span.</summary>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <returns>The code action title.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetTitle(Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, LayoutRules.NoBlankLinesAtStartOfFile.Id, StringComparison.Ordinal)
            ? RemoveLeadingBlankLinesTitle
            : RemoveExtraBlankLinesTitle;
}
